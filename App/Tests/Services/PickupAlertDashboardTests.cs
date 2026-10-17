using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Services
{
    public class PickupAlertDashboardTests
    {
        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<Booking> SeedAsync(RideReadyDbContext context, string reference, DateTime pickupInMalaysia, string status)
        {
            var customer = new Customer { Name = "Customer " + reference, Phone = "012" + Math.Abs(reference.GetHashCode() % 10000000).ToString("D7"), Email = reference + "@example.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = reference,
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = DateOnly.FromDateTime(pickupInMalaysia),
                PickupTime = TimeOnly.FromDateTime(pickupInMalaysia),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = status
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        [Fact]
        public async Task Dashboard_FlagsPickupSoonAndOverdue_AndLeavesOthersUnflagged()
        {
            var context = NewContext();
            var malaysiaNow = DateTime.UtcNow.AddHours(8);
            var soon = await SeedAsync(context, "RR-SOON", malaysiaNow.AddMinutes(60), "Confirmed");
            var overdue = await SeedAsync(context, "RR-LATE", malaysiaNow.AddMinutes(-60), "New");
            var later = await SeedAsync(context, "RR-LATER", malaysiaNow.AddDays(3), "New");
            var started = await SeedAsync(context, "RR-GONE", malaysiaNow.AddMinutes(-60), "Picked_Up");

            var items = await new DriverAssignmentService(context).GetDashboardBookingsAsync();

            string? AlertFor(Booking b) => items.Single(i => i.BookingId == b.Id).PickupAlert;
            Assert.Equal(DashboardAttention.PickupSoon, AlertFor(soon));
            Assert.Equal(DashboardAttention.PickupOverdue, AlertFor(overdue));
            Assert.Null(AlertFor(later));
            Assert.Null(AlertFor(started));
        }
    }
}
