using Microsoft.EntityFrameworkCore;
using Quartz;
using RideReady.Data;
using RideReady.Services;

namespace RideReady.Jobs
{
    public class ReminderEscalationJob : IJob
    {
        private readonly RideReadyDbContext _context;
        private readonly INotificationService _notificationService;

        public ReminderEscalationJob(RideReadyDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            await CheckWindowAsync(now, TimeSpan.FromHours(1), "Reminder_1hr", urgent: false);
            await CheckWindowAsync(now, TimeSpan.FromMinutes(30), "Escalation_30min", urgent: true);
        }

        private async Task CheckWindowAsync(DateTime now, TimeSpan window, string eventType, bool urgent)
        {
            var windowStart = now.Add(window).AddMinutes(-2);
            var windowEnd = now.Add(window).AddMinutes(2);

            var unassigned = await _context.Bookings
                .Where(b => b.Status == "New" || b.Status == "Confirmed")
                .ToListAsync();

            foreach (var booking in unassigned)
            {
                try
                {
                    var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
                    if (pickupAt < windowStart || pickupAt > windowEnd)
                    {
                        continue;
                    }

                    var alreadySent = await _context.Notifications
                        .AnyAsync(n => n.BookingId == booking.Id && n.EventType == eventType);
                    if (alreadySent)
                    {
                        continue;
                    }

                    await _notificationService.SendUnassignedReminderAsync(booking.Id, urgent);
                }
                catch (Exception)
                {
                    // Isolate this booking's failure so one bad record doesn't abort the whole
                    // batch. The reminder isn't marked as sent, so it will be picked up again
                    // (and retried) on the next 5-minute tick.
                }
            }
        }
    }
}
