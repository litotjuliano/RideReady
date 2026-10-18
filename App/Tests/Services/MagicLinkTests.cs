using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
{
    internal sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 18, 2, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public class MagicLinkExpiryTests
    {
        // 02:00 UTC on 18 Oct 2026 is 10:00 on 18 Oct in Malaysia.
        private static readonly DateTime UtcNow = new(2026, 10, 18, 2, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void LastsUntilTheEndOfTheDayAfterPickup_InMalaysiaTime()
        {
            var expires = MagicLinkExpiry.For(new DateOnly(2026, 10, 20), UtcNow);

            // 21 Oct 23:59:59 in Malaysia = 21 Oct 15:59:59 UTC
            Assert.Equal(new DateTime(2026, 10, 21, 15, 59, 59, DateTimeKind.Utc), expires);
        }

        [Fact]
        public void NeverLessThan24HoursFromNow_EvenForAPickupInThePast()
        {
            var expires = MagicLinkExpiry.For(new DateOnly(2026, 9, 12), UtcNow);

            Assert.Equal(UtcNow.AddHours(24), expires);
        }
    }

    public class MagicLinkServiceTests
    {
        private static MagicLinkService Create(TestClock clock, string baseUrl = "https://rideready.example", LinkSigningKey? key = null) =>
            new(key ?? LinkSigningKey.NewRandom(), Options.Create(new AppSettings { PublicBaseUrl = baseUrl }), clock);

        private static Booking BookingOn(DateOnly pickup, int id = 7) => new()
        {
            Id = id, BookingReference = "RR-LINK0001", PickupDate = pickup, PickupTime = new TimeOnly(10, 0),
            PickupLocation = "A", Destination = "B", RequestedVehicleType = "Car"
        };

        private static string TokenOf(string link) => link[(link.LastIndexOf("/l/", StringComparison.Ordinal) + 3)..];

        [Fact]
        public void AdminLink_OpensTheBookingAndCarriesNoPassword()
        {
            var service = Create(new TestClock());
            var link = service.LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!;

            Assert.StartsWith("https://rideready.example/l/", link);
            var read = service.Read(TokenOf(link));
            Assert.Equal(MagicLinkStatus.Valid, read.Status);
            Assert.Equal(MagicLinkRole.Admin, read.Link!.Role);
            Assert.Equal(7, read.Link.BookingId);
            Assert.DoesNotContain("admin", link, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DriverLink_CarriesTheDriverId()
        {
            var service = Create(new TestClock());
            var link = service.LinkForDriver(42, BookingOn(new DateOnly(2026, 10, 20)))!;

            var read = service.Read(TokenOf(link));
            Assert.Equal(MagicLinkStatus.Valid, read.Status);
            Assert.Equal(MagicLinkRole.Driver, read.Link!.Role);
            Assert.Equal(42, read.Link.DriverId);
        }

        [Fact]
        public void TrailingSlashInTheBaseUrlIsIgnored()
        {
            var link = Create(new TestClock(), "https://rideready.example/").LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!;

            Assert.StartsWith("https://rideready.example/l/", link);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void NoPublicAddress_MeansNoLink(string baseUrl)
        {
            var service = Create(new TestClock(), baseUrl);

            Assert.Null(service.LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20))));
            Assert.Null(service.LinkForDriver(1, BookingOn(new DateOnly(2026, 10, 20))));
        }

        [Fact]
        public void LinkWorksUntilTheEndOfTheDayAfterPickup_ThenExpires()
        {
            var clock = new TestClock();
            var service = Create(clock);
            var token = TokenOf(service.LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!);

            clock.Now = new DateTimeOffset(2026, 10, 21, 15, 59, 0, TimeSpan.Zero);
            Assert.Equal(MagicLinkStatus.Valid, service.Read(token).Status);

            clock.Now = new DateTimeOffset(2026, 10, 21, 16, 0, 1, TimeSpan.Zero);
            Assert.Equal(MagicLinkStatus.Expired, service.Read(token).Status);
        }

        [Fact]
        public void LinkCanBeUsedMoreThanOnce()
        {
            var service = Create(new TestClock());
            var token = TokenOf(service.LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!);

            Assert.Equal(MagicLinkStatus.Valid, service.Read(token).Status);
            Assert.Equal(MagicLinkStatus.Valid, service.Read(token).Status);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-token")]
        [InlineData("AAAA")]
        public void GarbageIsInvalid(string token)
        {
            Assert.Equal(MagicLinkStatus.Invalid, Create(new TestClock()).Read(token).Status);
        }

        [Fact]
        public void ChangedTokenIsInvalid()
        {
            var service = Create(new TestClock());
            var token = TokenOf(service.LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!);
            var middle = token.Length / 2;
            var tampered = token[..middle] + (token[middle] == (char)65 ? "B" : "A") + token[(middle + 1)..];

            Assert.Equal(MagicLinkStatus.Invalid, service.Read(tampered).Status);
        }

        [Fact]
        public void TokenFromAnotherKeyRingIsInvalid()
        {
            var token = TokenOf(Create(new TestClock()).LinkForAdmin(BookingOn(new DateOnly(2026, 10, 20)))!);

            // A different key ring (for example after the keys were rotated to revoke every link).
            Assert.Equal(MagicLinkStatus.Invalid, Create(new TestClock()).Read(token).Status);
        }
    }

    public class MagicLinkMessageTests
    {
        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<(RideReadyDbContext Context, Booking Booking)> SeedAsync()
        {
            var context = NewContext();
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            var booking = new Booking
            {
                BookingReference = "RR-LINK0002", CustomerId = customer.Id, PickupLocation = "KL Sentral", Destination = "KLIA T1",
                PickupDate = new DateOnly(2026, 10, 20), PickupTime = new TimeOnly(9, 0), Passengers = 1, Bags = 0,
                RequestedVehicleType = "Car", Status = "New"
            };
            context.Bookings.Add(booking);
            context.Drivers.Add(new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "T 1", PinHash = "x", IsActive = true });
            await context.SaveChangesAsync();
            return (context, booking);
        }

        private static NotificationService Notifier(RideReadyDbContext context, FakeEmailSender email, FakeWhatsAppSender whatsApp, string baseUrl)
        {
            var links = new MagicLinkService(LinkSigningKey.NewRandom(),
                Options.Create(new AppSettings { PublicBaseUrl = baseUrl }), new TestClock());
            return new NotificationService(context, email, whatsApp, new FakeCalendarSyncService(),
                Options.Create(new EmailSettings { OperatorEmail = "operator@rideready.my", SenderEmail = "n@r.my", SenderName = "RideReady" }),
                Options.Create(new WhatsAppSettings { ApiUrl = "x", AccessToken = "t", PhoneNumberId = "1", OperatorPhone = "0192462592" }),
                links);
        }

        [Fact]
        public async Task NewBooking_OperatorMessagesCarryALink_CustomerMessagesDoNot()
        {
            var (context, booking) = await SeedAsync();
            var email = new FakeEmailSender();
            var whatsApp = new FakeWhatsAppSender();

            await Notifier(context, email, whatsApp, "https://rideready.example").SendBookingCreatedNotificationAsync(booking.Id);

            Assert.Contains(whatsApp.Sent, s => s.To == "0192462592" && s.Message.Contains("https://rideready.example/l/"));
            Assert.Contains(email.Sent, s => s.To == "operator@rideready.my" && s.Body.Contains("https://rideready.example/l/"));
            Assert.All(whatsApp.Sent.Where(s => s.To == "0125183838"), s => Assert.DoesNotContain("/l/", s.Message));
            Assert.All(email.Sent.Where(s => s.To == "sim@email.com"), s => Assert.DoesNotContain("/l/", s.Body));
        }

        [Fact]
        public async Task WithoutAPublicAddress_MessagesAreExactlyAsBefore()
        {
            var (context, booking) = await SeedAsync();
            var whatsApp = new FakeWhatsAppSender();

            await Notifier(context, new FakeEmailSender(), whatsApp, "").SendBookingCreatedNotificationAsync(booking.Id);

            Assert.All(whatsApp.Sent, s => Assert.DoesNotContain("/l/", s.Message));
        }

        [Fact]
        public async Task DriverAssigned_DriverMessageHasTheirOwnLink()
        {
            var (context, booking) = await SeedAsync();
            var driver = await context.Drivers.FirstAsync();
            var whatsApp = new FakeWhatsAppSender();

            await Notifier(context, new FakeEmailSender(), whatsApp, "https://rideready.example").SendDriverAssignedNotificationAsync(booking.Id, driver.Id);

            var message = whatsApp.Sent.Single(s => s.To == driver.Phone).Message;
            Assert.Contains("https://rideready.example/l/", message);
            Assert.DoesNotContain("Log in to the Driver Portal", message);
        }

        [Fact]
        public async Task DriverAccepted_AndTripUpdates_AndCompleted_GiveTheAdminALink()
        {
            var (context, booking) = await SeedAsync();
            var whatsApp = new FakeWhatsAppSender();
            var notifier = Notifier(context, new FakeEmailSender(), whatsApp, "https://rideready.example");

            await notifier.SendDriverAcceptedNotificationAsync(booking.Id);
            await notifier.SendTripStatusUpdateNotificationAsync(booking.Id, "Picked_Up");
            await notifier.SendBookingCompletedNotificationAsync(booking.Id);

            var toOperator = whatsApp.Sent.Where(s => s.To == "0192462592").ToList();
            Assert.Equal(3, toOperator.Count);
            Assert.All(toOperator, s => Assert.Contains("/l/", s.Message));
        }
    }

    public class MagicLinkControllerTests
    {
        private sealed class RecordingAuth : IAuthenticationService
        {
            public List<(string? Scheme, ClaimsPrincipal Principal)> SignIns { get; } = new();
            public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
            public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
            public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
            public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            {
                SignIns.Add((scheme, principal));
                return Task.CompletedTask;
            }
            public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        }

        private sealed class StubUrlHelper : Microsoft.AspNetCore.Mvc.IUrlHelper
        {
            public ActionContext ActionContext => new();
            public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext) => "/stub";
            public string? Content(string? contentPath) => contentPath;
            public bool IsLocalUrl(string? url) => true;
            public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext) => "/stub";
            public string? Link(string? routeName, object? values) => "/stub";
        }

        private sealed class EmptyTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static (MagicLinkController Controller, RecordingAuth Auth, MagicLinkService Links, TestClock Clock) Build(RideReadyDbContext context)
        {
            var clock = new TestClock();
            var links = new MagicLinkService(LinkSigningKey.NewRandom(),
                Options.Create(new AppSettings { PublicBaseUrl = "https://rideready.example" }), clock);
            var auth = new RecordingAuth();
            var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
            var controller = new MagicLinkController(links, context,
                Options.Create(new AdminCredentialsSettings { Username = "hytadmin", Password = "secret" }))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
            };
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(controller.HttpContext, new EmptyTempData());
            controller.Url = new StubUrlHelper();
            return (controller, auth, links, clock);
        }

        private static Booking BookingOn(int id = 5) => new()
        {
            Id = id, BookingReference = "RR-LINK0003", PickupDate = new DateOnly(2026, 10, 20), PickupTime = new TimeOnly(9, 0),
            PickupLocation = "A", Destination = "B", RequestedVehicleType = "Car"
        };

        private static string TokenOf(string link) => link[(link.LastIndexOf("/l/", StringComparison.Ordinal) + 3)..];

        [Fact]
        public async Task AdminLink_SignsInTheAdmin_AndOpensThatBookingOnTheDashboard()
        {
            var (controller, auth, links, _) = Build(NewContext());

            var result = await controller.Open(TokenOf(links.LinkForAdmin(BookingOn(5))!));

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Admin", redirect.ControllerName);
            Assert.Equal(5, redirect.RouteValues!["focus"]);
            Assert.Equal("AdminAuth", auth.SignIns.Single().Scheme);
            Assert.Equal("hytadmin", auth.SignIns.Single().Principal.Identity!.Name);
            Assert.Equal("no-store", controller.Response.Headers["Cache-Control"]);
            Assert.Equal("no-referrer", controller.Response.Headers["Referrer-Policy"]);
        }

        [Fact]
        public async Task DriverLink_SignsInThatDriver_AndOpensTheirTrips()
        {
            var context = NewContext();
            var driver = new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "T 1", PinHash = "x", IsActive = true };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            var (controller, auth, links, _) = Build(context);

            var result = await controller.Open(TokenOf(links.LinkForDriver(driver.Id, BookingOn())!));

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Driver", redirect.ControllerName);
            var signIn = auth.SignIns.Single();
            Assert.Equal("DriverAuth", signIn.Scheme);
            Assert.Equal(driver.Id.ToString(), signIn.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        [Fact]
        public async Task LinkForAnInactiveDriver_DoesNotSignIn()
        {
            var context = NewContext();
            var driver = new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "T 1", PinHash = "x", IsActive = false };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            var (controller, auth, links, _) = Build(context);

            var result = await controller.Open(TokenOf(links.LinkForDriver(driver.Id, BookingOn())!));

            Assert.IsType<ViewResult>(result);
            Assert.Equal(410, controller.Response.StatusCode);
            Assert.Empty(auth.SignIns);
        }

        [Fact]
        public async Task LinkForADriverWhoNoLongerExists_DoesNotSignIn()
        {
            var (controller, auth, links, _) = Build(NewContext());

            var result = await controller.Open(TokenOf(links.LinkForDriver(999, BookingOn())!));

            Assert.IsType<ViewResult>(result);
            Assert.Empty(auth.SignIns);
        }

        [Fact]
        public async Task ExpiredLink_ShowsTheFriendlyPage_AndDoesNotSignIn()
        {
            var (controller, auth, links, clock) = Build(NewContext());
            var token = TokenOf(links.LinkForAdmin(BookingOn())!);
            clock.Now = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

            var result = await controller.Open(token);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Expired", view.ViewName);
            Assert.Equal(410, controller.Response.StatusCode);
            Assert.True((bool)controller.ViewBag.Expired);
            Assert.Empty(auth.SignIns);
        }

        [Fact]
        public async Task GarbageLink_ShowsTheFriendlyPage_AndDoesNotSignIn()
        {
            var (controller, auth, _, _) = Build(NewContext());

            var result = await controller.Open("definitely-not-a-token");

            Assert.IsType<ViewResult>(result);
            Assert.False((bool)controller.ViewBag.Expired);
            Assert.Empty(auth.SignIns);
        }
    }
}

namespace RideReady.Tests.Services
{
    public class DevOutboxTests
    {
        private sealed class FakeEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
        {
            public FakeEnvironment(string name) { EnvironmentName = name; }
            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; } = "RideReady";
            public string ContentRootPath { get; set; } = string.Empty;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        public async Task OutsideDevelopment_TheOutboxDoesNotExist(string environment)
        {
            var controller = new DevController(NewContext(), new FakeEnvironment(environment));

            Assert.IsType<NotFoundResult>(await controller.Outbox());
        }

        [Fact]
        public async Task InDevelopment_TheOutboxListsMessages()
        {
            var controller = new DevController(NewContext(), new FakeEnvironment("Development"));

            Assert.IsType<ViewResult>(await controller.Outbox());
        }
    }
}

namespace RideReady.Tests.Services
{
    public class MagicLinkLengthAndKeyTests
    {
        private static Booking BookingOn(int id = 1234) => new()
        {
            Id = id, BookingReference = "RR-LINK0004", PickupDate = new DateOnly(2026, 10, 20), PickupTime = new TimeOnly(9, 0),
            PickupLocation = "A", Destination = "B", RequestedVehicleType = "Car"
        };

        private static MagicLinkService Service(LinkSigningKey? key = null) =>
            new(key ?? LinkSigningKey.NewRandom(), Options.Create(new AppSettings { PublicBaseUrl = "https://rideready.litxus.com" }), new TestClock());

        [Fact]
        public void TheLinksAreShort()
        {
            var admin = Service().LinkForAdmin(BookingOn())!;
            var driver = Service().LinkForDriver(5, BookingOn())!;

            Assert.True(admin.Length <= 70, $"admin link is {admin.Length} characters: {admin}");
            Assert.True(driver.Length <= 70, $"driver link is {driver.Length} characters: {driver}");
        }

        [Fact]
        public void TheCodeUsesOnlyLettersDigitsDashAndUnderscore()
        {
            var link = Service().LinkForDriver(5, BookingOn())!;
            var code = link[(link.LastIndexOf("/l/", StringComparison.Ordinal) + 3)..];

            Assert.Matches("^[A-Za-z0-9_-]+$", code);
        }

        [Fact]
        public void ADriverLinkCannotBePassedOffAsAnAdminLink()
        {
            var service = Service();
            var driverLink = service.LinkForDriver(5, BookingOn())!;
            var code = driverLink[(driverLink.LastIndexOf("/l/", StringComparison.Ordinal) + 3)..];

            Assert.Equal(MagicLinkRole.Driver, service.Read(code).Link!.Role);
        }

        [Fact]
        public void TheKeyIsKeptInTheFolderAndReusedAfterARestart()
        {
            var folder = Path.Combine(Path.GetTempPath(), "rr-key-" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = LinkSigningKey.LoadOrCreate(folder);
                var second = LinkSigningKey.LoadOrCreate(folder);

                Assert.Equal(first.Value, second.Value);

                // Deleting the key file is how every link already sent is cancelled.
                File.Delete(Path.Combine(folder, "link-signing.key"));
                Assert.NotEqual(first.Value, LinkSigningKey.LoadOrCreate(folder).Value);
            }
            finally
            {
                if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
            }
        }

        [Fact]
        public void WithoutAFolderTheKeyIsFreshEachTime()
        {
            Assert.NotEqual(LinkSigningKey.LoadOrCreate(null).Value, LinkSigningKey.LoadOrCreate("").Value);
        }

        [Fact]
        public void ATooShortKeyIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new LinkSigningKey(new byte[4]));
        }
    }
}
