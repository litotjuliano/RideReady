using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Jobs;
using RideReady.Models;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Jobs
{
    public class NoShowDetectionJobTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideReadyDbContext context, DateTime pickupAtUtc, string status)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0007",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = DateOnly.FromDateTime(pickupAtUtc),
                PickupTime = TimeOnly.FromDateTime(pickupAtUtc),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = status
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private INotificationService BuildNotificationService(RideReadyDbContext context) =>
            new NotificationService(context, new RideReady.Tests.Services.FakeEmailSender(), new RideReady.Tests.Services.FakeWhatsAppSender(),
                new RideReady.Tests.Services.FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@rideready.my", SenderName = "RideReady", OperatorEmail = "operator@rideready.my" }));

        // Decorates a real INotificationService but throws for one specific booking, simulating
        // a downstream failure (e.g. a booking whose customer record is missing) that the job's
        // per-item try/catch must isolate so it doesn't abort processing of the other eligible
        // bookings in the batch.
        private class ThrowingForBookingNotificationService : INotificationService
        {
            private readonly INotificationService _inner;
            private readonly int _failingBookingId;

            public ThrowingForBookingNotificationService(INotificationService inner, int failingBookingId)
            {
                _inner = inner;
                _failingBookingId = failingBookingId;
            }

            public Task SendBookingCreatedNotificationAsync(int bookingId) => _inner.SendBookingCreatedNotificationAsync(bookingId);
            public Task SendDriverAssignedNotificationAsync(int bookingId, int driverId) => _inner.SendDriverAssignedNotificationAsync(bookingId, driverId);
            public Task SendDriverAcceptedNotificationAsync(int bookingId) => _inner.SendDriverAcceptedNotificationAsync(bookingId);
            public Task SendBookingCompletedNotificationAsync(int bookingId) => _inner.SendBookingCompletedNotificationAsync(bookingId);
            public Task SendBookingCancelledNotificationAsync(int bookingId) => _inner.SendBookingCancelledNotificationAsync(bookingId);
            public Task SendUnassignedReminderAsync(int bookingId, bool urgent) => _inner.SendUnassignedReminderAsync(bookingId, urgent);

            public Task SendNoShowNotificationAsync(int bookingId)
            {
                if (bookingId == _failingBookingId)
                {
                    throw new InvalidOperationException("Simulated downstream failure");
                }
                return _inner.SendNoShowNotificationAsync(bookingId);
            }
        }

        [Fact]
        public async Task RunAsync_WithBookingNotPickedUp40MinutesAfterPickupTime_MarksAsNoShow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Driver_Assigned");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("No_Show", updated!.Status);
            var history = await context.BookingStatusHistories.FirstOrDefaultAsync(h => h.BookingId == booking.Id);
            Assert.NotNull(history);
            Assert.Equal("No_Show", history!.NewStatus);
        }

        [Fact]
        public async Task RunAsync_WithBookingAlreadyPickedUp_DoesNotChangeStatus()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Picked_Up");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Picked_Up", updated!.Status);
        }

        [Fact]
        public async Task RunAsync_WithinTheThirtyMinuteGracePeriod_DoesNotMarkAsNoShow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-10), "Driver_Assigned");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
        }

        [Fact]
        public async Task RunAsync_WhenOneBookingsNotificationThrows_StillProcessesTheOtherBooking()
        {
            // Arrange: two eligible bookings past the grace period; booking A's no-show
            // notification throws (simulating a downstream failure). Booking B must still be
            // marked No_Show and notified despite A's failure.
            var context = GetInMemoryDbContext();
            var bookingA = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Driver_Assigned");
            var bookingB = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Driver_Assigned");
            var notificationService = new ThrowingForBookingNotificationService(BuildNotificationService(context), bookingA.Id);
            var job = new NoShowDetectionJob(context, notificationService);

            // Act
            await job.RunAsync();

            // Assert
            var updatedB = await context.Bookings.FindAsync(bookingB.Id);
            Assert.Equal("No_Show", updatedB!.Status);
            var historyB = await context.BookingStatusHistories.FirstOrDefaultAsync(h => h.BookingId == bookingB.Id);
            Assert.NotNull(historyB);
        }
    }
}
