using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task SendAsync(string toEmail, string subject, string body)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("SMTP unavailable");
            }
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    public class FakeWhatsAppSender : IWhatsAppSender
    {
        public List<(string To, string Message)> Sent { get; } = new();

        public Task SendAsync(string toPhone, string message)
        {
            Sent.Add((toPhone, message));
            return Task.CompletedTask;
        }
    }

    public class FakeCalendarSyncService : ICalendarSyncService
    {
        public int CallCount { get; private set; }

        public Task CreateOrUpdateEventAsync(Booking booking)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    public class NotificationServiceTests
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
                BookingReference = "RR-TEST0004",
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
            return booking;
        }

        private static IOptions<EmailSettings> Settings() => Options.Create(new EmailSettings
        {
            SenderEmail = "noreply@ridebooking.my",
            SenderName = "RideBooking",
            OperatorEmail = "operator@ridebooking.my"
        });

        [Fact]
        public async Task SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var calendarSync = new FakeCalendarSyncService();
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings());

            // Act
            await service.SendBookingCreatedNotificationAsync(booking.Id);

            // Assert
            Assert.Equal(2, emailSender.Sent.Count);
            Assert.Contains(emailSender.Sent, s => s.To == "sim@email.com");
            Assert.Contains(emailSender.Sent, s => s.To == "operator@ridebooking.my");
            Assert.Equal(1, calendarSync.CallCount);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(3, notifications.Count);
            Assert.All(notifications, n => Assert.Equal("Sent", n.DeliveryStatus));
        }

        [Fact]
        public async Task SendBookingCreatedNotificationAsync_WhenEmailFails_LogsFailedNotificationAndDoesNotThrow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var emailSender = new FakeEmailSender { ShouldThrow = true };
            var whatsAppSender = new FakeWhatsAppSender();
            var calendarSync = new FakeCalendarSyncService();
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings());

            // Act
            await service.SendBookingCreatedNotificationAsync(booking.Id);

            // Assert (no exception thrown)
            var failed = await context.Notifications
                .Where(n => n.BookingId == booking.Id && n.Channel == "Email")
                .ToListAsync();
            Assert.All(failed, n => Assert.Equal("Failed", n.DeliveryStatus));
            Assert.All(failed, n => Assert.Equal("SMTP unavailable", n.ErrorMessage));
        }

        [Fact]
        public async Task SendDriverAssignedNotificationAsync_SendsWhatsAppToDriverAndEmailToOperator()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings());

            // Act
            await service.SendDriverAssignedNotificationAsync(booking.Id, driver.Id);

            // Assert
            Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0123456789", whatsAppSender.Sent[0].To);
            Assert.Single(emailSender.Sent);
        }
    }
}
