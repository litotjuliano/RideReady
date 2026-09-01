using Microsoft.EntityFrameworkCore;
using Quartz;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;

namespace RideBooking.Jobs
{
    public class NoShowDetectionJob : IJob
    {
        private static readonly string[] EligibleStatuses = { "New", "Confirmed", "Driver_Assigned" };
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

        private readonly RideBookingDbContext _context;
        private readonly INotificationService _notificationService;

        public NoShowDetectionJob(RideBookingDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.Bookings
                .Where(b => EligibleStatuses.Contains(b.Status))
                .ToListAsync();

            foreach (var booking in candidates)
            {
                var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
                if (now - pickupAt < GracePeriod)
                {
                    continue;
                }

                var previousStatus = booking.Status;
                booking.Status = "No_Show";
                booking.UpdatedAt = now;

                _context.BookingStatusHistories.Add(new BookingStatusHistory
                {
                    BookingId = booking.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = "No_Show",
                    ChangedBy = "System"
                });

                await _context.SaveChangesAsync();
                await _notificationService.SendNoShowNotificationAsync(booking.Id);
            }
        }
    }
}
