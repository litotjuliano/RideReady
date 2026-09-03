using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Services
{
    public class DriverAvailabilityServiceTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Driver> SeedDriverAsync(RideReadyDbContext context, string name = "Ah Seng", string phone = "0123456789")
        {
            var driver = new Driver
            {
                Name = name,
                Phone = phone,
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                PinHash = "hash"
            };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            return driver;
        }

        private async Task<Booking> SeedBookingAsync(
            RideReadyDbContext context, string reference, DateOnly pickupDate, TimeOnly pickupTime, string status = "Confirmed")
        {
            var customer = new Customer { Name = "Customer " + reference, Phone = "01" + Math.Abs(reference.GetHashCode() % 100000000), Email = "c@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = reference,
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = pickupDate,
                PickupTime = pickupTime,
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = status
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private async Task AssignAsync(RideReadyDbContext context, Booking booking, Driver driver, string assignmentStatus = "Accepted")
        {
            context.DriverAssignments.Add(new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = assignmentStatus
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForDriverWithNoAssignments_ReturnsEmptyBlocksAndNotOnTimeOff()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var entry = Assert.Single(schedule);
            Assert.Empty(entry.Blocks);
            Assert.False(entry.IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForAssignmentWithQuoteDuration_UsesQuoteDurationForBlockEnd()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-QUOTED01", date, new TimeOnly(9, 0));
            context.BookingQuotes.Add(new BookingQuote { BookingId = booking.Id, DurationHours = 1.5m, TotalEstimatedFare = 100m, PaymentMethod = "Pay_at_Pickup" });
            await context.SaveChangesAsync();
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var block = Assert.Single(schedule[0].Blocks);
            Assert.Equal(new TimeOnly(9, 0), block.Start);
            Assert.Equal(new TimeOnly(10, 30), block.End);
            Assert.Equal("RR-QUOTED01", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForAssignmentWithoutQuoteDuration_UsesTwoHourFallback()
        {
            // Arrange — booking has no BookingQuote row at all (manual-fare path never set one)
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-NOQUOTE1", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var block = Assert.Single(schedule[0].Blocks);
            Assert.Equal(new TimeOnly(11, 0), block.End);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ExcludesCancelledAndRejectedAssignments()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var cancelled = await SeedBookingAsync(context, "RR-CANCELLED", date, new TimeOnly(9, 0), status: "Cancelled");
            await AssignAsync(context, cancelled, driver);
            var rejected = await SeedBookingAsync(context, "RR-REJECTED1", date, new TimeOnly(11, 0));
            await AssignAsync(context, rejected, driver, assignmentStatus: "Rejected");

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            Assert.Empty(schedule[0].Blocks);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForDriverOnTimeOff_SetsIsOnTimeOffAndReason()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            context.DriverTimeOffs.Add(new DriverTimeOff { DriverId = driver.Id, StartDate = new DateOnly(2026, 9, 9), EndDate = new DateOnly(2026, 9, 11), Reason = "Sick leave" });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            Assert.True(schedule[0].IsOnTimeOff);
            Assert.Equal("Sick leave", schedule[0].TimeOffReason);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ExcludesInactiveDrivers()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            driver.IsActive = false;
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(new DateOnly(2026, 9, 10));

            // Assert
            Assert.Empty(schedule);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForLateNightAssignmentWithoutQuoteDuration_ClampsBlockEndAtEndOfDay()
        {
            // Arrange — 23:00 pickup + 2h fallback would wrap past midnight to 01:00 if not clamped
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-LATENIGHT", date, new TimeOnly(23, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var block = Assert.Single(schedule[0].Blocks);
            Assert.Equal(new TimeOnly(23, 59), block.End);

            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(23, 30));
            Assert.False(isAvailable);
            Assert.Contains("RR-LATENIGHT", reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenDriverHasNoBookingsThatDay_ReturnsAvailable()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

            // Assert
            Assert.True(isAvailable);
            Assert.Null(reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenTimeFallsInsideAnExistingBlock_ReturnsBusyWithBookingReference()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-BUSY0001", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act — 10:00 falls inside the default 09:00-11:00 fallback block
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(10, 0));

            // Assert
            Assert.False(isAvailable);
            Assert.Contains("RR-BUSY0001", reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenDriverOnTimeOff_ReturnsBusyWithTimeOffReason()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            context.DriverTimeOffs.Add(new DriverTimeOff { DriverId = driver.Id, StartDate = date, EndDate = date });
            await context.SaveChangesAsync();

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(9, 0));

            // Assert
            Assert.False(isAvailable);
            Assert.Equal("on time off", reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenExcludingTheConflictingBookingItself_ReturnsAvailable()
        {
            // Arrange — reassigning the same booking to the same driver shouldn't flag it as busy with itself
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-SELF0001", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(9, 0), excludeBookingId: booking.Id);

            // Assert
            Assert.True(isAvailable);
            Assert.Null(reason);
        }
    }
}
