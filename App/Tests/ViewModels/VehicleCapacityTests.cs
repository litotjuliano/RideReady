using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class VehicleCapacityTests
    {
        private static readonly IReadOnlyDictionary<string, int> Defaults = VehicleCapacity.DefaultCapacities();

        private static (string VehicleType, int? PassengerCapacity, bool IsActive) D(string type, int? seats, bool active = true) =>
            (type, seats, active);

        // ---- defaults ----

        [Fact]
        public void Defaults_AreCar4_Van8_Bus8()
        {
            Assert.Equal(4, Defaults["Car"]);
            Assert.Equal(8, Defaults["Van"]);
            Assert.Equal(8, Defaults["Bus"]);
            Assert.Equal(new[] { "Car", "Van", "Bus" }, VehicleCapacity.Types);
        }

        [Fact]
        public void NoDrivers_UsesTheDefaults()
        {
            var result = VehicleCapacity.FromDrivers(Array.Empty<(string, int?, bool)>());

            Assert.Equal(Defaults, result);
        }

        // ---- limit comes from the drivers ----

        [Fact]
        public void TheLimitIsTheLargestCapacityAmongActiveDriversOfThatType()
        {
            var result = VehicleCapacity.FromDrivers(new[] { D("Van", 7), D("Van", 14), D("Van", 10), D("Car", 5) });

            Assert.Equal(14, result["Van"]);
            Assert.Equal(5, result["Car"]);
            Assert.Equal(8, result["Bus"]); // no Bus driver: default
        }

        [Fact]
        public void InactiveDrivers_DoNotCount()
        {
            var result = VehicleCapacity.FromDrivers(new[] { D("Van", 14, active: false), D("Van", 7) });

            Assert.Equal(7, result["Van"]);
        }

        [Fact]
        public void DriversWithoutARecordedCapacity_AreIgnored_AndTheDefaultApplies()
        {
            var result = VehicleCapacity.FromDrivers(new[] { D("Van", null), D("Bus", 0), D("Car", null) });

            Assert.Equal(Defaults, result);
        }

        [Fact]
        public void ADriverCanLowerTheLimitBelowTheDefault()
        {
            // A 2-seat "Car" is the only Car driver: the limit follows the real vehicle, not the default of 4.
            var result = VehicleCapacity.FromDrivers(new[] { D("Car", 2) });

            Assert.Equal(2, result["Car"]);
        }

        // ---- the message ----

        [Theory]
        [InlineData("Car", 1)]
        [InlineData("Car", 4)]
        [InlineData("Van", 8)]
        public void WithinTheLimit_HasNoMessage(string vehicle, int passengers)
        {
            Assert.Null(VehicleCapacity.ExceededMessage(Defaults, vehicle, passengers));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Helicopter")]
        public void UnknownVehicle_HasNoLimit(string? vehicle)
        {
            Assert.Null(VehicleCapacity.MaxFor(Defaults, vehicle));
            Assert.Null(VehicleCapacity.ExceededMessage(Defaults, vehicle, 99));
        }

        [Fact]
        public void CarWithFivePassengers_SaysTheLimitAndSuggestsBiggerVehicles()
        {
            Assert.Equal("A Car carries up to 4 passengers. Choose a Van or Bus.", VehicleCapacity.ExceededMessage(Defaults, "Car", 5));
        }

        [Fact]
        public void OnlyVehiclesBigEnoughAreSuggested()
        {
            var capacities = VehicleCapacity.FromDrivers(new[] { D("Van", 8), D("Bus", 30) });

            Assert.Equal("A Van carries up to 8 passengers. Choose a Bus.", VehicleCapacity.ExceededMessage(capacities, "Van", 12));
        }

        [Fact]
        public void TooManyForEveryVehicle_AsksTheCustomerToContactUs()
        {
            Assert.Equal("A Van carries up to 8 passengers. Please contact us for a larger group.",
                VehicleCapacity.ExceededMessage(Defaults, "Van", 9));
        }

        // ---- the booking page rule, end to end ----

        private static RideReadyDbContext NewContext() => new(new DbContextOptionsBuilder<RideReadyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        private static async Task AddDriverAsync(RideReadyDbContext context, string type, int? seats, bool active = true)
        {
            context.Drivers.Add(new Driver
            {
                Name = $"{type} driver {seats}", Phone = "0123456789", VehicleType = type, VehicleNumber = "ABC 123",
                PassengerCapacity = seats, IsActive = active, PinHash = "x"
            });
            await context.SaveChangesAsync();
        }

        private static BookingRequestViewModel Request(string vehicle, int passengers) => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            CustomerEmail = "sim@email.com",
            FromName = "KLIA T1",
            ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
            Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            PickupTime = new TimeOnly(12, 0),
            DropOffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            DropOffTime = new TimeOnly(15, 0),
            Passengers = passengers,
            Bags = 1,
            VehicleType = vehicle,
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        [Fact]
        public async Task BookingService_ReadsTheLimitsFromTheDrivers()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Van", 7);
            await AddDriverAsync(context, "Van", 14);

            var capacities = await new BookingService(context).GetVehicleCapacitiesAsync();

            Assert.Equal(14, capacities["Van"]);
            Assert.Equal(4, capacities["Car"]);
        }

        [Fact]
        public async Task BookingService_AllowsMoreThanTheDefaultWhenADriversVehicleIsBigger()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Van", 14);

            var booking = await new BookingService(context).CreateBookingAsync(Request("Van", 12));

            Assert.Equal(12, booking.Passengers);
        }

        [Fact]
        public async Task BookingService_RefusesMoreThanTheLargestVehicleOfThatType()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Car", 5);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new BookingService(context).CreateBookingAsync(Request("Car", 6)));

            Assert.Contains("carries up to 5 passengers", ex.Message);
            Assert.Empty(context.Bookings);
        }

        [Fact]
        public async Task BookingService_FallsBackToTheDefaultsWhenNoCapacityIsRecorded()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Car", null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new BookingService(context).CreateBookingAsync(Request("Car", 5)));

            Assert.Contains("carries up to 4 passengers", ex.Message);
        }

        private static BookingController NewBookingController(RideReadyDbContext context)
        {
            var notifier = new NotificationService(context, new RideReady.Tests.Services.FakeEmailSender(),
                new RideReady.Tests.Services.FakeWhatsAppSender(), new RideReady.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings { SenderEmail = "n@r.my", SenderName = "RideReady", OperatorEmail = "o@r.my" }),
                Microsoft.Extensions.Options.Options.Create(new WhatsAppSettings { ApiUrl = "https://x", AccessToken = "t", PhoneNumberId = "1", OperatorPhone = "0192462592" }));
            var controller = new BookingController(new BookingService(context), notifier);
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(), new RideReady.Tests.Controllers.NullTempDataProvider());
            return controller;
        }

        [Fact]
        public async Task Controller_PutsTheCapacityErrorOnThePassengersField()
        {
            var context = NewContext();
            var controller = NewBookingController(context);

            var result = await controller.Create(Request("Car", 5));

            var view = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            var error = Assert.Single(controller.ModelState[nameof(BookingRequestViewModel.Passengers)]!.Errors);
            Assert.Equal("A Car carries up to 4 passengers. Choose a Van or Bus.", error.ErrorMessage);
            Assert.NotNull(view.ViewData["VehicleCapacities"]);
            Assert.Empty(context.Bookings);
        }

        [Fact]
        public async Task Controller_AcceptsTheBookingWhenTheVehicleIsBigEnough()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Van", 14);
            var controller = NewBookingController(context);

            var result = await controller.Create(Request("Van", 12));

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Single(context.Bookings);
        }

        [Fact]
        public async Task FrontPage_GetsTheSameLimitsAsTheBookingForm()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Van", 14);
            var controller = new HomeController(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeController>.Instance, new BookingService(context));

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var capacities = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(view.ViewData["VehicleCapacities"]);
            Assert.Equal(14, capacities["Van"]);
            Assert.Equal("~/Views/Booking/Create.cshtml", view.ViewName);
        }

        [Fact]
        public async Task Controller_FormGetsTheLimitsPerVehicleType()
        {
            var context = NewContext();
            await AddDriverAsync(context, "Bus", 30);
            var controller = NewBookingController(context);

            var result = await controller.Create();

            var capacities = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(Assert.IsType<ViewResult>(result).ViewData["VehicleCapacities"]);
            Assert.Equal(30, capacities["Bus"]);
            Assert.Equal(4, capacities["Car"]);
        }
    }
}
