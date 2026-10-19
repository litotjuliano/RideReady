using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class OtherVehicleTests
    {
        private static BookingRequestViewModel Valid(string vehicle = "Car", string? other = null) => new()
        {
            CustomerName = "Uncle Sim", CustomerPhone = "012-3456789", CustomerEmail = "sim@email.com",
            FromName = "KLIA T1", ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang", Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), PickupTime = new TimeOnly(12, 0),
            DropOffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), DropOffTime = new TimeOnly(15, 0),
            Passengers = 2, Bags = 1, VehicleType = vehicle, OtherVehicle = other,
            AcceptedTerms = true
        };

        private static string? MessageFor(BookingRequestViewModel model, string member)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.FirstOrDefault(r => r.MemberNames.Contains(member))?.ErrorMessage;
        }

        private static RideReadyDbContext NewContext() => new(new DbContextOptionsBuilder<RideReadyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        // ---- the form ----

        [Fact]
        public void OthersNeedsADescription()
        {
            Assert.Equal("Please tell us what vehicle you need.", MessageFor(Valid("Others", null), "OtherVehicle"));
            Assert.Equal("Please tell us what vehicle you need.", MessageFor(Valid("Others", "   "), "OtherVehicle"));
        }

        [Fact]
        public void OthersWithADescription_IsValid()
        {
            Assert.Null(MessageFor(Valid("Others", "lorry"), "OtherVehicle"));
            Assert.Null(MessageFor(Valid("Others", "lorry"), "VehicleType"));
        }

        [Fact]
        public void TheDescriptionIsOnlyAskedForWithOthers()
        {
            Assert.Null(MessageFor(Valid("Car", null), "OtherVehicle"));
            Assert.Null(MessageFor(Valid("Van", "ignored"), "OtherVehicle"));
        }

        [Fact]
        public void TheDescriptionIsKeptShort()
        {
            Assert.Equal("Please keep the vehicle description under 100 characters.", MessageFor(Valid("Others", new string('x', 101)), "OtherVehicle"));
        }

        [Theory]
        [InlineData("Car")]
        [InlineData("Van")]
        [InlineData("Bus")]
        [InlineData("Others")]
        public void TheFourVehicleChoicesAreAccepted(string vehicle)
        {
            Assert.Null(MessageFor(Valid(vehicle, "x"), "VehicleType"));
        }

        // ---- no passenger limit ----

        [Fact]
        public void OthersHasNoPassengerLimit()
        {
            var capacities = VehicleCapacity.DefaultCapacities();

            Assert.Null(VehicleCapacity.MaxFor(capacities, "Others"));
            Assert.Null(VehicleCapacity.ExceededMessage(capacities, "Others", 60));
        }

        [Fact]
        public void OtherTypesStillHaveTheirLimit()
        {
            Assert.NotNull(VehicleCapacity.ExceededMessage(VehicleCapacity.DefaultCapacities(), "Car", 60));
        }

        [Fact]
        public void OthersIsNeverSuggestedAsABiggerVehicle()
        {
            var message = VehicleCapacity.ExceededMessage(VehicleCapacity.DefaultCapacities(), "Car", 6)!;

            Assert.DoesNotContain("Others", message);
        }

        // ---- saving the booking ----

        [Fact]
        public async Task TheBookingKeepsTheDescription_ForOthers()
        {
            var context = NewContext();
            var booking = await new BookingService(context).CreateBookingAsync(Valid("Others", "  lorry (10 ft)  "));

            Assert.Equal("Others", booking.RequestedVehicleType);
            Assert.Equal("lorry (10 ft)", booking.OtherVehicleDescription);
        }

        [Fact]
        public async Task ABookingForALargeGroupInOthers_IsAccepted()
        {
            var context = NewContext();
            var request = Valid("Others", "coach");
            request.Passengers = 40;

            var booking = await new BookingService(context).CreateBookingAsync(request);

            Assert.Equal(40, booking.Passengers);
        }

        [Fact]
        public async Task AOrdinaryBookingStoresNoDescription()
        {
            var context = NewContext();
            var booking = await new BookingService(context).CreateBookingAsync(Valid("Van", "left over text"));

            Assert.Null(booking.OtherVehicleDescription);
        }

        // ---- assigning a driver ----

        [Fact]
        public async Task OthersBookingCanBeAssignedToAnyDriver_WhateverTheSeats()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", VehicleNumber = "ABC 1234", Pin = "1234", PassengerCapacity = 4
            });
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            var booking = new Booking
            {
                BookingReference = "RR-OTHERS01", CustomerId = customer.Id, PickupLocation = "A", Destination = "B",
                PickupDate = new DateOnly(2026, 10, 20), PickupTime = new TimeOnly(9, 0), Passengers = 30, Bags = 0,
                RequestedVehicleType = "Others", OtherVehicleDescription = "coach", Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            await service.AssignDriverAsync(booking.Id, driver.Id);

            Assert.Equal("Driver_Assigned", (await context.Bookings.FindAsync(booking.Id))!.Status);
        }

        // ---- what people read ----

        [Theory]
        [InlineData("Car", null, "Car")]
        [InlineData("Others", "lorry", "Others: lorry")]
        [InlineData("Others", "  lorry  ", "Others: lorry")]
        [InlineData("Others", null, "Others")]
        [InlineData("Others", "  ", "Others")]
        [InlineData(null, null, "")]
        public void TheVehicleReadsNaturally(string? type, string? other, string expected)
        {
            Assert.Equal(expected, TripText.Vehicle(type, other));
        }

        [Fact]
        public void TheTripDetailsShowTheDescription_ToAdminAndToDriver()
        {
            var admin = TripDetailsViewModel.ForAdmin(new AdminBookingListItemViewModel
            {
                Passengers = 12, Bags = 3, RequestedVehicleType = "Others", OtherVehicle = "lorry"
            });
            var driver = TripDetailsViewModel.ForDriver(new DriverAssignmentListItemViewModel
            {
                Passengers = 12, Bags = 3, RequestedVehicleType = "Others", OtherVehicle = "lorry"
            });

            Assert.Contains(admin.Rows(), r => r.Label == "Passengers & vehicle" && r.Value == "12 pax · 3 bags · Others: lorry");
            Assert.Contains(driver.Rows(), r => r.Label == "Passengers & vehicle" && r.Value == "12 pax · 3 bags · Others: lorry");
        }

        [Fact]
        public void TheDashboardSearchFindsTheDescription()
        {
            var text = DashboardFilter.SearchText(new AdminBookingListItemViewModel { CustomerName = "A", OtherVehicle = "Refrigerated Lorry" });

            Assert.Contains("refrigerated lorry", text);
        }
    }
}
