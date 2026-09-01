using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class DriverPortalServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<(Driver Driver, Booking Booking, DriverAssignment Assignment)> SeedAssignedBookingAsync(RideBookingDbContext context)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);

            var driver = new Driver
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                PinHash = PasswordHasher.Hash("1234")
            };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0002",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = "Driver_Assigned"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var assignment = new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = "Pending"
            };
            context.DriverAssignments.Add(assignment);
            await context.SaveChangesAsync();

            return (driver, booking, assignment);
        }

        [Fact]
        public async Task AuthenticateAsync_WithCorrectPin_ReturnsDriver()
        {
            var context = GetInMemoryDbContext();
            var (driver, _, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.AuthenticateAsync(driver.Phone, "1234");

            Assert.NotNull(result);
            Assert.Equal(driver.Id, result!.Id);
        }

        [Fact]
        public async Task AuthenticateAsync_WithWrongPin_ReturnsNull()
        {
            var context = GetInMemoryDbContext();
            var (driver, _, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.AuthenticateAsync(driver.Phone, "0000");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAssignmentsAsync_ReturnsOnlyThatDriversAssignments()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.GetAssignmentsAsync(driver.Id);

            var item = Assert.Single(result);
            Assert.Equal(booking.BookingReference, item.BookingReference);
        }

        [Fact]
        public async Task AcceptAssignmentAsync_SetsAssignmentAcceptedAndBookingConfirmed()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var bookingId = await service.AcceptAssignmentAsync(assignment.Id, driver.Id);

            var updatedAssignment = await context.DriverAssignments.FindAsync(assignment.Id);
            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Accepted", updatedAssignment!.AssignmentStatus);
            Assert.NotNull(updatedAssignment.AcceptedAt);
            Assert.Equal("Confirmed", updatedBooking!.Status);
            Assert.Equal(booking.Id, bookingId);
        }

        [Fact]
        public async Task AcceptAssignmentAsync_ForADifferentDriver_ThrowsInvalidOperationException()
        {
            var context = GetInMemoryDbContext();
            var (_, _, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AcceptAssignmentAsync(assignment.Id, driverId: 9999));
        }

        [Fact]
        public async Task AcceptAssignmentAsync_WhenBookingWasCancelled_ThrowsInvalidOperationException()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            booking.Status = "Cancelled";
            await context.SaveChangesAsync();
            var service = new DriverPortalService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AcceptAssignmentAsync(assignment.Id, driver.Id));

            var updatedAssignment = await context.DriverAssignments.FindAsync(assignment.Id);
            Assert.Equal("Pending", updatedAssignment!.AssignmentStatus);
        }

        [Fact]
        public async Task GetAssignmentsAsync_ExcludesCancelledBookings()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, _) = await SeedAssignedBookingAsync(context);
            booking.Status = "Cancelled";
            await context.SaveChangesAsync();
            var service = new DriverPortalService(context);

            var result = await service.GetAssignmentsAsync(driver.Id);

            Assert.Empty(result);
        }

        [Fact]
        public async Task RejectAssignmentAsync_SetsAssignmentRejectedAndBookingBackToNew()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await service.RejectAssignmentAsync(assignment.Id, driver.Id);

            var updatedAssignment = await context.DriverAssignments.FindAsync(assignment.Id);
            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Rejected", updatedAssignment!.AssignmentStatus);
            Assert.Equal("New", updatedBooking!.Status);
        }

        [Fact]
        public async Task UpdateTripStatusAsync_WithAcceptedAssignment_UpdatesBookingStatus()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);
            await service.AcceptAssignmentAsync(assignment.Id, driver.Id);

            await service.UpdateTripStatusAsync(booking.Id, driver.Id, "Picked_Up");

            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Picked_Up", updatedBooking!.Status);
        }

        [Fact]
        public async Task RecordLocationAsync_PersistsALocationRow()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await service.RecordLocationAsync(driver.Id, booking.Id, 3.1390m, 101.6869m, 15, 42.5m);

            var count = await context.DriverLocations.CountAsync(l => l.DriverId == driver.Id);
            Assert.Equal(1, count);
        }
    }
}
