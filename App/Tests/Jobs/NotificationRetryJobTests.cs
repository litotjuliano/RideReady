using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Jobs;
using RideReady.Models;
using RideReady.Tests.Services;
using Xunit;

namespace RideReady.Tests.Jobs
{
    public class NotificationRetryJobTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Notification> SeedFailedNotificationAsync(RideReadyDbContext context, int retryCount, DateTime lastAttemptAt, string channel = "Email")
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0005",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var notification = new Notification
            {
                BookingId = booking.Id,
                RecipientType = "Customer",
                RecipientContact = "sim@email.com",
                Channel = channel,
                EventType = "BookingCreated",
                Subject = "Your RideReady reservation",
                MessageContent = "Hi Uncle Sim",
                DeliveryStatus = "Failed",
                RetryCount = retryCount,
                LastAttemptAt = lastAttemptAt
            };
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            return notification;
        }

        [Fact]
        public async Task RunAsync_WhenBackoffElapsedAndResendSucceeds_MarksAsSent()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 0, lastAttemptAt: DateTime.UtcNow.AddMinutes(-10));
            var emailSender = new FakeEmailSender();
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("Sent", updated!.DeliveryStatus);
            Assert.Equal(1, updated.RetryCount);
            Assert.Single(emailSender.Sent);
        }

        [Fact]
        public async Task RunAsync_WhenBackoffNotYetElapsed_DoesNotRetry()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 0, lastAttemptAt: DateTime.UtcNow.AddMinutes(-1));
            var emailSender = new FakeEmailSender();
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("Failed", updated!.DeliveryStatus);
            Assert.Equal(0, updated.RetryCount);
            Assert.Empty(emailSender.Sent);
        }

        [Fact]
        public async Task RunAsync_OnTheFourthRetryStillFailing_MarksAsDeadLetter()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 3, lastAttemptAt: DateTime.UtcNow.AddHours(-4));
            var emailSender = new FakeEmailSender { ShouldThrow = true };
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("DeadLetter", updated!.DeliveryStatus);
            Assert.Equal(4, updated.RetryCount);
        }

        [Fact]
        public async Task RunAsync_WithUnsupportedChannel_DoesNotMarkAsSentAndDeadLettersInstead()
        {
            // Arrange: this job only knows how to resend Email and WhatsApp; a "Calendar"
            // notification (logged by NotificationService.SendBookingCreatedNotificationAsync)
            // has no supported retry path here.
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 0, lastAttemptAt: DateTime.UtcNow.AddMinutes(-10), channel: "Calendar");
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var job = new NotificationRetryJob(context, emailSender, whatsAppSender);

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.NotEqual("Sent", updated!.DeliveryStatus);
            Assert.Equal("DeadLetter", updated.DeliveryStatus);
            Assert.False(string.IsNullOrEmpty(updated.ErrorMessage));
            Assert.Empty(emailSender.Sent);
            Assert.Empty(whatsAppSender.Sent);
        }
    }
}
