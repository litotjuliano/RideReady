using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Jobs
{
    public class ReminderEscalationJobTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedUnassignedBookingAsync(RideBookingDbContext context, DateTime pickupAtUtc)
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

        private INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(context, new RideBooking.Tests.Services.FakeEmailSender(), new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@ridebooking.my", SenderName = "RideBooking", OperatorEmail = "operator@ridebooking.my" }));

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
    }
}
