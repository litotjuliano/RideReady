using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;

namespace RideBooking.Services
{
    public class NotificationService : INotificationService
    {
        private readonly RideBookingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;

        public NotificationService(
            RideBookingDbContext context,
            IEmailSender emailSender,
            IWhatsAppSender whatsAppSender,
            ICalendarSyncService calendarSyncService,
            IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
            _calendarSyncService = calendarSyncService;
            _emailSettings = emailSettings.Value;
        }

        public async Task SendBookingCreatedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var customerMessage = $"Hi {booking.Customer!.Name}, your RideBooking reference is {booking.BookingReference}. We'll contact you to confirm your driver.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCreated", customerMessage,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideBooking reservation", customerMessage));

            var operatorMessage = $"New booking {booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}.";
            await SendAndLogAsync(bookingId, "Operator", null, "Email", "BookingCreated", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, "Calendar", "BookingCreated", "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }

        public async Task SendDriverAssignedNotificationAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");
            var driver = await _context.Drivers.FindAsync(driverId)
                ?? throw new InvalidOperationException($"Driver {driverId} not found");

            var driverMessage = $"New job {booking.BookingReference}: pickup {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}. Log in to the Driver Portal to accept or reject.";
            await SendAndLogAsync(bookingId, "Driver", driverId, "WhatsApp", "DriverAssigned", driverMessage,
                () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));

            var operatorMessage = $"Driver {driver.Name} assigned to booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Operator", null, "Email", "DriverAssigned", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "Driver assigned", operatorMessage));
        }

        public async Task SendDriverAcceptedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Good news! A driver has been confirmed for your booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "DriverAccepted", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Driver confirmed", message));
        }

        public async Task SendBookingCompletedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Thanks for riding with RideBooking! Your trip {booking.BookingReference} is complete.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCompleted", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Trip complete", message));
        }

        public async Task SendBookingCancelledNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} has been cancelled.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCancelled", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Booking cancelled", message));

            var latestDriverId = await _context.DriverAssignments
                .Where(a => a.BookingId == bookingId && a.AssignmentStatus != "Rejected")
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => (int?)a.DriverId)
                .FirstOrDefaultAsync();

            if (latestDriverId != null)
            {
                var driver = await _context.Drivers.FindAsync(latestDriverId.Value);
                if (driver != null)
                {
                    var driverMessage = $"Booking {booking.BookingReference} has been cancelled. No action needed.";
                    await SendAndLogAsync(bookingId, "Driver", driver.Id, "WhatsApp", "BookingCancelled", driverMessage,
                        () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));
                }
            }
        }

        private async Task SendAndLogAsync(
            int bookingId, string recipientType, int? recipientId, string channel, string eventType,
            string messageContent, Func<Task> send)
        {
            var notification = new Notification
            {
                BookingId = bookingId,
                RecipientType = recipientType,
                RecipientId = recipientId,
                Channel = channel,
                EventType = eventType,
                MessageContent = messageContent,
                DeliveryStatus = "Pending"
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                await send();
                notification.DeliveryStatus = "Sent";
                notification.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.DeliveryStatus = "Failed";
                notification.ErrorMessage = ex.Message;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // The channel send above already ran (and may have succeeded) — a failure to persist
                // the final Sent/Failed status is a notification-logging problem, not a failure of the
                // action that triggered this notification. Don't let it bubble up and turn an already-
                // successful booking/assignment/etc. into an unhandled error for the caller.
            }
        }
    }
}
