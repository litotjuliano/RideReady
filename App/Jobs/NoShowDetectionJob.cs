using Microsoft.EntityFrameworkCore;
using Quartz;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;

namespace RideReady.Jobs
{
    public class NoShowDetectionJob : IJob
    {
        private static readonly string[] EligibleStatuses = { "New", "Confirmed", "Driver_Assigned" };
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

        private readonly RideReadyDbContext _context;
        private readonly INotificationService _notificationService;

        public NoShowDetectionJob(RideReadyDbContext context, INotificationService notificationService)
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
                try
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
                catch (Exception)
                {
                    // Isolate this booking's failure so one bad record (malformed data, a
                    // downstream notification error, etc.) doesn't abort the whole batch.
                    // If the status change above didn't persist, this booking is still
                    // eligible and will be re-evaluated on the next 5-minute tick.
                }
            }
        }
    }
}
