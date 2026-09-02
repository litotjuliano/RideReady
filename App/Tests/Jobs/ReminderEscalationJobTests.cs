using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Jobs;
using RideReady.Models;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Jobs
{
    public class ReminderEscalationJobTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Booking> SeedUnassignedBookingAsync(RideReadyDbContext context, DateTime pickupAtUtc)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0006",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = DateOnly.FromDateTime(pickupAtUtc),
                PickupTime = TimeOnly.FromDateTime(pickupAtUtc),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
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
        // a downstream failure (e.g. a malformed record) that the job's per-item try/catch must
        // isolate so it doesn't abort processing of the other eligible bookings in the batch.
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
            public Task SendNoShowNotificationAsync(int bookingId) => _inner.SendNoShowNotificationAsync(bookingId);

            public Task SendUnassignedReminderAsync(int bookingId, bool urgent)
            {
                if (bookingId == _failingBookingId)
                {
                    throw new InvalidOperationException("Simulated downstream failure");
                }
                return _inner.SendUnassignedReminderAsync(bookingId, urgent);
            }
        }

        [Fact]
        public async Task RunAsync_WithUnassignedBookingOneHourOut_SendsReminderOnce()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();
            await job.RunAsync(); // second run should not duplicate

            // Assert
            var count = await context.Notifications.CountAsync(n => n.EventType == "Reminder_1hr");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task RunAsync_WithUnassignedBookingThirtyMinutesOut_SendsUrgentEscalation()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddMinutes(30));
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var count = await context.Notifications.CountAsync(n => n.EventType == "Escalation_30min");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task RunAsync_WithAssignedBooking_DoesNotSendReminder()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            booking.Status = "Driver_Assigned";
            await context.SaveChangesAsync();
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var count = await context.Notifications.CountAsync(n => n.BookingId == booking.Id);
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task RunAsync_WhenOneBookingsReminderThrows_StillProcessesTheOtherBooking()
        {
            // Arrange: two eligible bookings in the same window; booking A's notification send
            // throws (simulating a bad record / downstream failure). Booking B must still be
            // processed despite A's failure.
            var context = GetInMemoryDbContext();
            var bookingA = await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            var bookingB = await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            var notificationService = new ThrowingForBookingNotificationService(BuildNotificationService(context), bookingA.Id);
            var job = new ReminderEscalationJob(context, notificationService);

            // Act
            await job.RunAsync();

            // Assert
            var countA = await context.Notifications.CountAsync(n => n.BookingId == bookingA.Id && n.EventType == "Reminder_1hr");
            var countB = await context.Notifications.CountAsync(n => n.BookingId == bookingB.Id && n.EventType == "Reminder_1hr");
            Assert.Equal(0, countA);
            Assert.Equal(1, countB);
        }
    }
}
