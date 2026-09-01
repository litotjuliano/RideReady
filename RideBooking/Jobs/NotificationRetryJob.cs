using Microsoft.EntityFrameworkCore;
using Quartz;
using RideBooking.Data;
using RideBooking.Services;

namespace RideBooking.Jobs
{
    public class NotificationRetryJob : IJob
    {
        private static readonly TimeSpan[] BackoffDelays =
        {
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(3)
        };

        private readonly RideBookingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;

        public NotificationRetryJob(RideBookingDbContext context, IEmailSender emailSender, IWhatsAppSender whatsAppSender)
        {
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.Notifications
                .Where(n => n.DeliveryStatus == "Failed" && n.RetryCount < 4)
                .ToListAsync();

            foreach (var notification in candidates)
            {
                var delay = BackoffDelays[notification.RetryCount];
                if (notification.LastAttemptAt == null || now - notification.LastAttemptAt.Value < delay)
                {
                    continue;
                }

                notification.RetryCount++;
                notification.LastAttemptAt = now;

                try
                {
                    if (notification.Channel == "Email")
                    {
                        await _emailSender.SendAsync(notification.RecipientContact, notification.Subject ?? notification.EventType, notification.MessageContent ?? string.Empty);
                    }
                    else if (notification.Channel == "WhatsApp")
                    {
                        await _whatsAppSender.SendAsync(notification.RecipientContact, notification.MessageContent ?? string.Empty);
                    }
                    else
                    {
                        // This job only knows how to resend Email and WhatsApp. Channels such as
                        // "Calendar" have no supported retry path here (it doesn't depend on
                        // ICalendarSyncService) — dead-letter immediately instead of silently
                        // falling through to "Sent", which would hide a sync that never happened.
                        notification.ErrorMessage = $"Retry not supported for channel '{notification.Channel}'";
                        notification.DeliveryStatus = "DeadLetter";
                        continue;
                    }

                    notification.DeliveryStatus = "Sent";
                    notification.SentAt = now;
                }
                catch (Exception ex)
                {
                    notification.ErrorMessage = ex.Message;
                    notification.DeliveryStatus = notification.RetryCount >= 4 ? "DeadLetter" : "Failed";
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
