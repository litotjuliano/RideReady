using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class PhoneLinksTests
    {
        [Theory]
        [InlineData("0123456789", "60123456789")]
        [InlineData("012-3456789", "60123456789")]
        [InlineData("012-345 6789", "60123456789")]
        [InlineData("+60123456789", "60123456789")]
        [InlineData("60123456789", "60123456789")]
        [InlineData("01110883795", "601110883795")]
        [InlineData("  0 12 345 6789  ", "60123456789")]
        public void ToInternational_GivesDigitsWithTheCountryCode(string input, string expected)
        {
            Assert.Equal(expected, PhoneLinks.ToInternational(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        public void NoUsableNumber_GivesNoLink(string? input)
        {
            Assert.Equal(string.Empty, PhoneLinks.Tel(input));
            Assert.Equal(string.Empty, PhoneLinks.WhatsApp(input));
        }

        [Fact]
        public void Links_UseTheInternationalNumber()
        {
            Assert.Equal("tel:+60123456789", PhoneLinks.Tel("012-345 6789"));
            Assert.Equal("https://wa.me/60123456789", PhoneLinks.WhatsApp("0123456789"));
        }

        [Fact]
        public void WhatsAppSending_UsesTheSameNumberAsTheLinks()
        {
            Assert.Equal(PhoneLinks.ToInternational("012-345 6789"), WhatsAppCloudApiSender.NormalizePhone("012-345 6789"));
        }
    }

    public class DashboardFilterTests
    {
        [Theory]
        [InlineData("New", "new")]
        [InlineData("Driver_Assigned", "assigned")]
        [InlineData("Confirmed", "confirmed")]
        [InlineData("Picked_Up", "ontrip")]
        [InlineData("In_Transit", "ontrip")]
        [InlineData("Dropped_Off", "ontrip")]
        [InlineData("Completed", "other")]
        [InlineData("Cancelled", "other")]
        [InlineData("No_Show", "other")]
        public void StatusesBelongToTheRightChip(string status, string group)
        {
            Assert.Equal(group, DashboardFilter.StatusGroup(status));
        }

        [Fact]
        public void TheChipsAreInOrder()
        {
            Assert.Equal(new[] { "New", "Assigned", "Confirmed", "On trip" }, DashboardFilter.StatusChips.Select(c => c.Label));
        }

        [Fact]
        public void SearchText_CoversNamePhoneReferenceRouteAndDriver_InLowerCase()
        {
            var text = DashboardFilter.SearchText(new AdminBookingListItemViewModel
            {
                CustomerName = "Uncle SIM",
                CustomerPhone = "012-345 6789",
                BookingReference = "RR-ABC123",
                FromName = "KLIA T1",
                ToName = "Mid Valley",
                PickupLocation = "Level 3, KLIA Terminal 1",
                Destination = "Mid Valley Megamall",
                AssignedDriverName = "Richard"
            });

            Assert.Equal(text, text.ToLowerInvariant());
            Assert.Contains("uncle sim", text);
            Assert.Contains("rr-abc123", text);
            Assert.Contains("klia t1", text);
            Assert.Contains("mid valley megamall", text);
            Assert.Contains("richard", text);
        }

        [Theory]
        [InlineData("0123456789")]
        [InlineData("012-345 6789")]
        [InlineData("60123456789")]
        public void SearchText_FindsAPhoneNumberWhicheverWayItIsTyped(string typed)
        {
            var text = DashboardFilter.SearchText(new AdminBookingListItemViewModel { CustomerPhone = "012-345 6789" });
            var digits = new string(typed.Where(char.IsDigit).ToArray());

            Assert.Contains(digits, text);
        }

        [Fact]
        public void SearchText_IgnoresMissingParts()
        {
            var text = DashboardFilter.SearchText(new AdminBookingListItemViewModel { CustomerName = "A" });

            Assert.DoesNotContain("  ", text);
        }
    }

    public class AttentionFlagTests
    {
        // 02:00 UTC on 20 Oct 2026 is 10:00 in Malaysia. A pickup far in the future raises no pickup flag.
        private static readonly DateTime UtcNow = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly Later = new(2026, 10, 25);
        private static readonly TimeOnly Noon = new(12, 0);

        private static string[] Keys(string status, string? assignment, bool detailsSent = false, DateOnly? date = null, TimeOnly? time = null) =>
            DashboardAttention.FlagsFor(status, assignment, detailsSent, date ?? Later, time ?? Noon, UtcNow).Select(f => f.Key).ToArray();

        [Fact]
        public void NewBookingWithoutADriver_NeedsOne()
        {
            Assert.Equal(new[] { "needs-driver" }, Keys("New", null));
        }

        [Fact]
        public void NewBookingWhoseDriverDeclined_NeedsANewOne()
        {
            Assert.Equal(new[] { "needs-driver" }, Keys("New", "Rejected"));
        }

        [Fact]
        public void AssignedButUnanswered_IsWaitingForTheDriver()
        {
            Assert.Equal(new[] { "waiting-driver" }, Keys("Driver_Assigned", "Pending"));
        }

        [Fact]
        public void AcceptedButNotSentToTheCustomer_NeedsTheDetailsSent()
        {
            Assert.Equal(new[] { "send-details" }, Keys("Confirmed", "Accepted", detailsSent: false));
        }

        [Fact]
        public void AcceptedAndSent_NeedsNothing()
        {
            Assert.Empty(Keys("Confirmed", "Accepted", detailsSent: true));
        }

        [Theory]
        [InlineData("Picked_Up", "Accepted")]
        [InlineData("In_Transit", "Accepted")]
        [InlineData("Dropped_Off", "Accepted")]
        [InlineData("Completed", "Accepted")]
        [InlineData("Cancelled", null)]
        [InlineData("No_Show", "Accepted")]
        public void StartedOrClosedBookings_RaiseNoFlags(string status, string? assignment)
        {
            Assert.Empty(Keys(status, assignment, detailsSent: false));
        }

        [Fact]
        public void PickupFlagsComeFirst_ThenWhatToDo()
        {
            // pickup in 30 minutes (10:30 Malaysia) and no driver yet
            var keys = Keys("New", null, date: new DateOnly(2026, 10, 20), time: new TimeOnly(10, 30));

            Assert.Equal(new[] { "pickup-soon", "needs-driver" }, keys);
        }

        [Fact]
        public void OverduePickupWithoutADriver_ShowsBoth()
        {
            var keys = Keys("New", null, date: new DateOnly(2026, 10, 20), time: new TimeOnly(8, 0));

            Assert.Equal(new[] { "pickup-overdue", "needs-driver" }, keys);
        }

        [Fact]
        public void EveryFlagHasAKeyAndALabel_AndTheKeysAreUnique()
        {
            Assert.Equal(5, DashboardAttention.AllFlags.Count);
            Assert.All(DashboardAttention.AllFlags, f => { Assert.NotEmpty(f.Key); Assert.NotEmpty(f.Label); });
            Assert.Equal(DashboardAttention.AllFlags.Count, DashboardAttention.AllFlags.Select(f => f.Key).Distinct().Count());
        }
    }

    public class AttentionFlagDashboardTests
    {
        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task Dashboard_CarriesTheFlagsForEachBooking()
        {
            var context = NewContext();
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            Booking Make(string reference, string status) => new()
            {
                BookingReference = reference, CustomerId = customer.Id, PickupLocation = "KL Sentral", Destination = "KLIA T1",
                PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), PickupTime = new TimeOnly(12, 0),
                Passengers = 1, Bags = 0, RequestedVehicleType = "Car", Status = status
            };

            var noDriver = Make("RR-NODRIVER", "New");
            var waiting = Make("RR-WAITING", "Driver_Assigned");
            var accepted = Make("RR-ACCEPTED", "Confirmed");
            context.Bookings.AddRange(noDriver, waiting, accepted);
            var driver = new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "T 1", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            context.DriverAssignments.Add(new DriverAssignment { BookingId = waiting.Id, DriverId = driver.Id, AssignmentStatus = "Pending" });
            context.DriverAssignments.Add(new DriverAssignment { BookingId = accepted.Id, DriverId = driver.Id, AssignmentStatus = "Accepted" });
            await context.SaveChangesAsync();

            var items = await new DriverAssignmentService(context).GetDashboardBookingsAsync();

            string[] FlagsOf(Booking b) => items.Single(i => i.BookingId == b.Id).AttentionFlags.Select(f => f.Key).ToArray();
            Assert.Equal(new[] { "needs-driver" }, FlagsOf(noDriver));
            Assert.Equal(new[] { "waiting-driver" }, FlagsOf(waiting));
            Assert.Equal(new[] { "send-details" }, FlagsOf(accepted));
        }
    }
}
