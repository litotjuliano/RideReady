using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Jobs
{
    public class NoShowDetectionJobTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideBookingDbContext context, DateTime pickupAtUtc, string status)
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

        private INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(context, new RideBooking.Tests.Services.FakeEmailSender(), new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@ridebooking.my", SenderName = "RideBooking", OperatorEmail = "operator@ridebooking.my" }));

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
    }
}
