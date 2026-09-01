using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class DriverAssignmentServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideBookingDbContext context)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0001",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        [Fact]
        public async Task CreateDriverAsync_WithValidRequest_HashesThePin()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var request = new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            };

            // Act
            var driver = await service.CreateDriverAsync(request);

            // Assert
            Assert.NotEqual("1234", driver.PinHash);
            Assert.True(PasswordHasher.Verify("1234", driver.PinHash));
        }

        [Fact]
        public async Task AssignDriverAsync_WithNewAssignment_SetsBookingStatusToDriverAssigned()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
            var assignment = await context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == booking.Id && a.DriverId == driver.Id);
            Assert.NotNull(assignment);
            Assert.Equal("Pending", assignment!.AssignmentStatus);
        }

        [Fact]
        public async Task AssignDriverAsync_CalledTwiceForSameDriver_DoesNotDuplicateAssignment()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            await service.AssignDriverAsync(booking.Id, driver.Id);
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Assert
            var count = await context.DriverAssignments
                .CountAsync(a => a.BookingId == booking.Id && a.DriverId == driver.Id);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task GetDashboardBookingsAsync_ReturnsBookingsWithAssignmentInfo()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Act
            var result = await service.GetDashboardBookingsAsync();

            // Assert
            var item = Assert.Single(result);
            Assert.Equal("Ah Seng", item.AssignedDriverName);
            Assert.Equal("Driver_Assigned", item.Status);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_WritesStatusHistory()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);

            // Act
            await service.UpdateBookingStatusAsync(booking.Id, "Confirmed", "Admin");

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Confirmed", updated!.Status);
            var history = await context.BookingStatusHistories.FirstOrDefaultAsync(h => h.BookingId == booking.Id);
            Assert.NotNull(history);
            Assert.Equal("New", history!.PreviousStatus);
            Assert.Equal("Confirmed", history.NewStatus);
            Assert.Equal("Admin", history.ChangedBy);
        }
    }
}
