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

        [Fact]
        public async Task AssignDriverAsync_ReassignedToADifferentDriver_UpdatesExistingRowInsteadOfDuplicating()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driverA = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            var driverB = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Bob",
                Phone = "0198765432",
                VehicleType = "Car",
                VehicleNumber = "XYZ 5678",
                Pin = "5678"
            });

            // Act
            await service.AssignDriverAsync(booking.Id, driverA.Id);
            await service.AssignDriverAsync(booking.Id, driverB.Id);

            // Assert
            var assignments = await context.DriverAssignments
                .Where(a => a.BookingId == booking.Id)
                .ToListAsync();
            var assignment = Assert.Single(assignments);
            Assert.Equal(driverB.Id, assignment.DriverId);
        }

        [Fact]
        public async Task AssignDriverAsync_WithNonexistentBooking_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignDriverAsync(bookingId: 9999, driver.Id));
        }

        [Fact]
        public async Task AssignDriverAsync_WithNonexistentDriver_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignDriverAsync(booking.Id, driverId: 9999));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_WithInvalidStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateBookingStatusAsync(booking.Id, "NotARealStatus", "Admin"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_WithNonexistentBooking_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateBookingStatusAsync(bookingId: 9999, "Confirmed", "Admin"));
        }

        [Fact]
        public async Task AssignDriverAsync_WhenBookingAlreadyPickedUp_ThrowsAndDoesNotMutateExistingAssignment()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var originalDriver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            var otherDriver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Bob",
                Phone = "0198765432",
                VehicleType = "Car",
                VehicleNumber = "XYZ 5678",
                Pin = "5678"
            });

            // Original driver is assigned, accepts, and picks up the passenger.
            await service.AssignDriverAsync(booking.Id, originalDriver.Id);
            var assignment = await context.DriverAssignments.FirstAsync(a => a.BookingId == booking.Id);
            assignment.AssignmentStatus = "Accepted";
            assignment.AcceptedAt = DateTime.UtcNow;
            var trackedBooking = await context.Bookings.FindAsync(booking.Id);
            trackedBooking!.Status = "Picked_Up";
            await context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignDriverAsync(booking.Id, otherDriver.Id));

            var unchangedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Picked_Up", unchangedBooking!.Status);

            var unchangedAssignment = await context.DriverAssignments
                .SingleAsync(a => a.BookingId == booking.Id);
            Assert.Equal(originalDriver.Id, unchangedAssignment.DriverId);
            Assert.Equal("Accepted", unchangedAssignment.AssignmentStatus);
            Assert.NotNull(unchangedAssignment.AcceptedAt);
        }

        [Fact]
        public async Task AssignDriverAsync_WhenBookingConfirmedButNotPickedUp_StillSucceeds()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var originalDriver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            var replacementDriver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Bob",
                Phone = "0198765432",
                VehicleType = "Car",
                VehicleNumber = "XYZ 5678",
                Pin = "5678"
            });

            await service.AssignDriverAsync(booking.Id, originalDriver.Id);
            var trackedBooking = await context.Bookings.FindAsync(booking.Id);
            trackedBooking!.Status = "Confirmed";
            await context.SaveChangesAsync();

            // Act — original driver became unavailable before pickup; admin reassigns.
            await service.AssignDriverAsync(booking.Id, replacementDriver.Id);

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
            var assignment = await context.DriverAssignments.SingleAsync(a => a.BookingId == booking.Id);
            Assert.Equal(replacementDriver.Id, assignment.DriverId);
            Assert.Equal("Pending", assignment.AssignmentStatus);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_WithDriverAssignedTarget_ThrowsAndDoesNotCreateAssignmentOrChangeStatus()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateBookingStatusAsync(booking.Id, "Driver_Assigned", "Admin"));

            var unchangedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("New", unchangedBooking!.Status);
            var assignmentCount = await context.DriverAssignments.CountAsync(a => a.BookingId == booking.Id);
            Assert.Equal(0, assignmentCount);
        }
    }
}
