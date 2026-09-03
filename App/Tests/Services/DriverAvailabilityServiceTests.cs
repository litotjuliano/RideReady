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

        [Fact]
        public async Task AddTimeOffAsync_WithValidRange_CreatesTimeOffRow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act
            await service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), "Annual leave");

            // Assert
            var timeOff = await context.DriverTimeOffs.SingleAsync();
            Assert.Equal(driver.Id, timeOff.DriverId);
            Assert.Equal(new DateOnly(2026, 9, 20), timeOff.StartDate);
            Assert.Equal(new DateOnly(2026, 9, 22), timeOff.EndDate);
            Assert.Equal("Annual leave", timeOff.Reason);
        }

        [Fact]
        public async Task AddTimeOffAsync_WithEndDateBeforeStartDate_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 20), null));
        }

        [Fact]
        public async Task AddTimeOffAsync_WithNonexistentDriver_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(9999, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null));
        }

        [Fact]
        public async Task AddTimeOffAsync_WithConflictingActiveAssignment_ThrowsExceptionNamingTheBooking()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-TIMEOFF1", new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null));
            Assert.Contains("RR-TIMEOFF1", ex.Message);
        }

        [Fact]
        public async Task AddTimeOffAsync_IgnoresCancelledBookingsWhenCheckingConflicts()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-CANCELLED2", new DateOnly(2026, 9, 21), new TimeOnly(9, 0), status: "Cancelled");
            await AssignAsync(context, booking, driver);

            // Act
            await service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null);

            // Assert
            Assert.Equal(1, await context.DriverTimeOffs.CountAsync());
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_ReturnsOneEntryPerDayOfTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            await SeedDriverAsync(context);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.Equal(30, schedule.Count);
            Assert.True(schedule.ContainsKey(new DateOnly(2026, 9, 1)));
            Assert.True(schedule.ContainsKey(new DateOnly(2026, 9, 30)));
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_IncludesBookingOnFirstDayOfMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-FIRSTDAY", new DateOnly(2026, 9, 1), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var day1 = schedule[new DateOnly(2026, 9, 1)];
            var block = Assert.Single(day1[0].Blocks);
            Assert.Equal("RR-FIRSTDAY", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_IncludesBookingOnLastDayOfMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-LASTDAY0", new DateOnly(2026, 9, 30), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var lastDay = schedule[new DateOnly(2026, 9, 30)];
            var block = Assert.Single(lastDay[0].Blocks);
            Assert.Equal("RR-LASTDAY0", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_DriverWithTwoBookingsSameDay_BothBlocksInOneDayEntry()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 15);
            var morning = await SeedBookingAsync(context, "RR-MORNING1", date, new TimeOnly(6, 0));
            await AssignAsync(context, morning, driver);
            var evening = await SeedBookingAsync(context, "RR-EVENING1", date, new TimeOnly(20, 0));
            await AssignAsync(context, evening, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var day = schedule[date];
            Assert.Equal(2, day[0].Blocks.Count);
            Assert.Contains(day[0].Blocks, b => b.BookingReference == "RR-MORNING1");
            Assert.Contains(day[0].Blocks, b => b.BookingReference == "RR-EVENING1");
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_TimeOffStartingBeforeMonthAndEndingInsideIt_AppliesWithinTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            context.DriverTimeOffs.Add(new DriverTimeOff
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 8, 28),
                EndDate = new DateOnly(2026, 9, 3),
                Reason = "Carried over leave"
            });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.True(schedule[new DateOnly(2026, 9, 1)][0].IsOnTimeOff);
            Assert.True(schedule[new DateOnly(2026, 9, 3)][0].IsOnTimeOff);
            Assert.False(schedule[new DateOnly(2026, 9, 4)][0].IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_TimeOffStartingInsideMonthAndEndingAfterIt_AppliesWithinTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            context.DriverTimeOffs.Add(new DriverTimeOff
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 9, 28),
                EndDate = new DateOnly(2026, 10, 5),
                Reason = "Spills into next month"
            });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.True(schedule[new DateOnly(2026, 9, 28)][0].IsOnTimeOff);
            Assert.True(schedule[new DateOnly(2026, 9, 30)][0].IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_WithNoActiveDrivers_ReturnsEmptyListForEveryDay()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.Equal(30, schedule.Count);
            Assert.All(schedule.Values, day => Assert.Empty(day));
        }
    }
}
