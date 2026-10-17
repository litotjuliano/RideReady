using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class VehicleCapacityTests
    {
        // ---- the table ----

        [Fact]
        public void Car_CarriesUpToFour()
        {
            Assert.Equal(4, VehicleCapacity.MaxFor("Car"));
        }

        [Theory]
        [InlineData("Van")]
        [InlineData("Bus")]
        public void VanAndBus_AreNotStricterThanTheOverallLimit(string vehicle)
        {
            Assert.Equal(VehicleCapacity.Largest, VehicleCapacity.MaxFor(vehicle));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Helicopter")]
        public void UnknownVehicle_HasNoLimit(string? vehicle)
        {
            Assert.Null(VehicleCapacity.MaxFor(vehicle));
            Assert.Null(VehicleCapacity.ExceededMessage(vehicle, 99));
        }

        [Fact]
        public void Types_AreOfferedInOrder()
        {
            Assert.Equal(new[] { "Car", "Van", "Bus" }, VehicleCapacity.Types);
        }

        // ---- the message ----

        [Theory]
        [InlineData("Car", 1)]
        [InlineData("Car", 4)]
        [InlineData("Van", 8)]
        [InlineData("Bus", 8)]
        public void WithinTheLimit_HasNoMessage(string vehicle, int passengers)
        {
            Assert.Null(VehicleCapacity.ExceededMessage(vehicle, passengers));
        }

        [Fact]
        public void CarWithFivePassengers_SaysTheLimitAndSuggestsABiggerVehicle()
        {
            var message = VehicleCapacity.ExceededMessage("Car", 5);

            Assert.Equal("A Car carries up to 4 passengers. Choose a Van or Bus.", message);
        }

        [Fact]
        public void TooManyForEveryVehicle_AsksTheCustomerToContactUs()
        {
            var message = VehicleCapacity.ExceededMessage("Van", 9);

            Assert.Equal("A Van carries up to 8 passengers. Please contact us for a larger group.", message);
        }

        // ---- the form model ----

        private static BookingRequestViewModel Request(string vehicle, int passengers) => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            CustomerEmail = "sim@email.com",
            FromName = "KLIA T1",
            ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
            Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = new DateOnly(2026, 10, 20),
            PickupTime = new TimeOnly(12, 0),
            DropOffDate = new DateOnly(2026, 10, 20),
            DropOffTime = new TimeOnly(15, 0),
            Passengers = passengers,
            Bags = 1,
            VehicleType = vehicle,
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        private static List<ValidationResult> Validate(BookingRequestViewModel model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void Form_RejectsMorePassengersThanTheChosenVehicleCarries()
        {
            var results = Validate(Request("Car", 5));

            var error = Assert.Single(results);
            Assert.Contains(nameof(BookingRequestViewModel.Passengers), error.MemberNames);
            Assert.Equal("A Car carries up to 4 passengers. Choose a Van or Bus.", error.ErrorMessage);
        }

        [Theory]
        [InlineData("Car", 4)]
        [InlineData("Van", 6)]
        [InlineData("Bus", 8)]
        public void Form_AcceptsPassengersThatFit(string vehicle, int passengers)
        {
            Assert.Empty(Validate(Request(vehicle, passengers)));
        }

        [Fact]
        public void Form_StillRejectsMoreThanTheOverallLimit()
        {
            var results = Validate(Request("Bus", 9));

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(BookingRequestViewModel.Passengers)));
        }

        // ---- the booking service (a second line of defence for anything that bypasses the form) ----

        [Fact]
        public async Task BookingService_RefusesACarBookingForFivePassengers()
        {
            var context = new RideReadyDbContext(new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var request = Request("Car", 5);
            request.PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            request.DropOffDate = request.PickupDate;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new BookingService(context).CreateBookingAsync(request));

            Assert.Contains("carries up to 4 passengers", ex.Message);
            Assert.Empty(context.Bookings);
        }

        [Fact]
        public async Task BookingService_AcceptsAVanBookingForFivePassengers()
        {
            var context = new RideReadyDbContext(new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var request = Request("Van", 5);
            request.PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            request.DropOffDate = request.PickupDate;

            var booking = await new BookingService(context).CreateBookingAsync(request);

            Assert.Equal(5, booking.Passengers);
        }
    }
}
