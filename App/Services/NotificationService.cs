using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Models;
using RideReady.ViewModels;

namespace RideReady.Services
{
    public class NotificationService : INotificationService
    {
        /// <summary>EventType recorded when the admin manually sends driver details to the customer.</summary>
        public const string DriverDetailsEvent = "DriverDetailsSent";

        private readonly RideReadyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;
        private readonly WhatsAppSettings _whatsAppSettings;
        private readonly IMagicLinkService? _magicLinks;

        public NotificationService(
            RideReadyDbContext context,
            IEmailSender emailSender,
            IWhatsAppSender whatsAppSender,
            ICalendarSyncService calendarSyncService,
            IOptions<EmailSettings> emailSettings,
            IOptions<WhatsAppSettings> whatsAppSettings,
            IMagicLinkService? magicLinks = null)
        {
            _magicLinks = magicLinks;
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
            _calendarSyncService = calendarSyncService;
            _emailSettings = emailSettings.Value;
            _whatsAppSettings = whatsAppSettings.Value;
        }

        public async Task SendBookingCreatedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var customerMessage = $"Hi {booking.Customer!.Name}, your RideReady reference is {booking.BookingReference}. We'll contact you to confirm your driver.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Email, "Email", "BookingCreated",
                "Your RideReady reservation", customerMessage,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideReady reservation", customerMessage));

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "BookingCreated",
                null, customerMessage,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, customerMessage));

            var operatorMessage = WithLink($"New booking {booking.BookingReference}: {TripDetails(booking)}", "Open", _magicLinks?.LinkForAdmin(booking));
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "BookingCreated",
                "New booking received", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "BookingCreated",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Calendar", "BookingCreated",
                null, "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }

        public async Task SendDriverAssignedNotificationAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");
            var driver = await _context.Drivers.FindAsync(driverId)
                ?? throw new InvalidOperationException($"Driver {driverId} not found");

            var driverLink = _magicLinks?.LinkForDriver(driverId, booking);
            var driverMessage = driverLink != null
                ? $"New job {booking.BookingReference}: {TripDetails(booking)} Open your trips to accept or reject: {driverLink}"
                : $"New job {booking.BookingReference}: {TripDetails(booking)} Log in to the Driver Portal to accept or reject.";
            await SendAndLogAsync(bookingId, "Driver", driverId, driver.Phone, "WhatsApp", "DriverAssigned",
                null, driverMessage,
                () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));

            var operatorMessage = $"Driver {driver.Name} assigned to booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "DriverAssigned",
                "Driver assigned", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "Driver assigned", operatorMessage));
        }

        public async Task SendPriceSetNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer).Include(b => b.Quote)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var fare = booking.Quote?.TotalEstimatedFare ?? 0;
            var message = $"Hi {booking.Customer!.Name}, the fare for your RideReady booking {booking.BookingReference} is RM{fare:F2}. Thank you!";

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "PriceSet",
                null, message,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, message));
        }

        public async Task SendTripStatusUpdateNotificationAsync(int bookingId, string newStatus)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var (customerMessage, operatorMessage) = newStatus switch
            {
                "Picked_Up" => (
                    $"Your RideReady driver has picked you up for booking {booking.BookingReference}. Enjoy your ride!",
                    $"Booking {booking.BookingReference}: driver has picked up the customer."),
                "In_Transit" => (
                    $"You're on your way! Booking {booking.BookingReference} is now in transit.",
                    $"Booking {booking.BookingReference} is now in transit."),
                "Dropped_Off" => (
                    $"You've arrived! Thanks for riding with RideReady (booking {booking.BookingReference}).",
                    $"Booking {booking.BookingReference}: customer dropped off."),
                _ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Not a supported trip status update")
            };

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Phone, "WhatsApp", $"TripStatus_{newStatus}",
                null, customerMessage,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, customerMessage));

            operatorMessage = WithLink(operatorMessage, "Open", _magicLinks?.LinkForAdmin(booking));
            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", $"TripStatus_{newStatus}",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }

        public async Task SendDriverAcceptedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            // The customer is deliberately NOT messaged here: the admin reviews the acceptance and
            // sends the driver details to the customer manually (see SendDriverDetailsToCustomerAsync).
            var operatorMessage = WithLink($"Driver accepted booking {booking.BookingReference}. Send the driver details to the customer from the dashboard.", "Open", _magicLinks?.LinkForAdmin(booking));
            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "DriverAccepted",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }

        /// <summary>
        /// One-line trip summary used in the operator and driver messages: the route (short From/To names when
        /// given, otherwise the addresses), the pick up address and time, and the drop-off address with the
        /// customer's approximate drop-off time.
        /// </summary>
        private static string TripDetails(Booking booking)
        {
            var pickup = string.IsNullOrWhiteSpace(booking.PickupLocation) ? "Pick up" : $"Pick up: {booking.PickupLocation.Trim()}";
            var dropOffAt = TripText.DropOffAt(booking.DropOffDate, booking.DropOffTime);
            var dropOffTime = dropOffAt.Length > 0 ? $", approx. {dropOffAt}" : string.Empty;

            return $"{TripText.Route(booking.FromName, booking.PickupLocation, booking.ToName, booking.Destination)}. " +
                $"{pickup} on {booking.PickupDate:yyyy-MM-dd} at {booking.PickupTime:HH:mm}. " +
                $"Drop-off: {booking.Destination.Trim()}{dropOffTime}.";
        }

        /// <summary>Adds "Open: <link>" to a message for the admin or a driver; without a link the message is unchanged.</summary>
        private static string WithLink(string message, string label, string? link) =>
            string.IsNullOrEmpty(link) ? message : $"{message} {label}: {link}";

        /// <summary>
        /// The confirmation the customer gets once the driver has accepted: the driver, then the trip summary.
        /// Each item is its own short sentence on its own line, so it also reads well when WhatsApp flattens it to one line.
        /// </summary>
        internal static string DriverDetailsMessage(Booking booking, Driver driver, string fareText)
        {
            var vehicle = string.Join(" ", new[] { driver.VehicleType, driver.VehicleNumber }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
            var pickupPlace = string.IsNullOrWhiteSpace(booking.PickupLocation) ? string.Empty : $" · {booking.PickupLocation.Trim()}";
            var dropOffWhen = booking.DropOffDate is DateOnly d && booking.DropOffTime is TimeOnly t ? $"{TripText.When(d, t)} · " : string.Empty;

            var lines = new List<string>
            {
                $"Your RideReady booking {booking.BookingReference} is confirmed.",
                string.Empty,
                $"Driver: {driver.Name}.",
                $"Phone: {driver.Phone}.",
            };
            if (vehicle.Length > 0) lines.Add($"Vehicle: {vehicle}.");
            lines.Add(string.Empty);
            lines.Add($"Route: {TripText.Route(booking.FromName, booking.PickupLocation, booking.ToName, booking.Destination)}.");
            lines.Add($"Pick up: {TripText.When(booking.PickupDate, booking.PickupTime)}{pickupPlace}.");
            lines.Add($"Drop-off (approx.): {dropOffWhen}{booking.Destination.Trim()}.");
            lines.Add($"{TripText.PassengersAndVehicle(booking.Passengers, booking.Bags, booking.RequestedVehicleType, booking.OtherVehicleDescription)}.");
            if (fareText.Length > 0) lines.Add(fareText.Trim());
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The same details as <see cref="DriverDetailsMessage"/>, one value per variable of the WhatsApp driver-details template
        /// (a variable cannot hold a line break, so the template body puts each on its own line):
        /// 1 booking, 2 driver, 3 phone, 4 vehicle, 5 route, 6 pick up time, 7 pick up place, 8 drop-off time,
        /// 9 drop-off place, 10 passengers line, 11 fare. A missing value is left empty and sent as a dash.
        /// </summary>
        internal static IReadOnlyList<string> DriverDetailsFields(Booking booking, Driver driver, decimal? fare) => new[]
        {
            booking.BookingReference,
            driver.Name,
            driver.Phone,
            string.Join(" ", new[] { driver.VehicleType, driver.VehicleNumber }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim())),
            TripText.Route(booking.FromName, booking.PickupLocation, booking.ToName, booking.Destination),
            TripText.When(booking.PickupDate, booking.PickupTime),
            booking.PickupLocation?.Trim() ?? string.Empty,
            booking.DropOffDate is DateOnly d && booking.DropOffTime is TimeOnly t ? TripText.When(d, t) : string.Empty,
            booking.Destination.Trim(),
            TripText.PassengersAndVehicle(booking.Passengers, booking.Bags, booking.RequestedVehicleType, booking.OtherVehicleDescription),
            fare is > 0 ? $"RM{fare:F2}" : "To be advised"
        };

        public async Task<bool> SendDriverDetailsToCustomerAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var assignment = await _context.DriverAssignments
                .Include(a => a.Driver)
                .Where(a => a.BookingId == bookingId && a.AssignmentStatus == "Accepted")
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            if (assignment?.Driver == null)
            {
                throw new InvalidOperationException("The driver hasn't accepted this booking yet, so there are no driver details to send");
            }

            var driver = assignment.Driver;

            // Mention the fare only when the admin has set one; with no price, the message says nothing about it.
            var quote = await _context.BookingQuotes.FirstOrDefaultAsync(q => q.BookingId == bookingId);
            var fareText = quote != null && quote.TotalEstimatedFare > 0
                ? $" Fare: RM{quote.TotalEstimatedFare:F2}."
                : string.Empty;
            var message = DriverDetailsMessage(booking, driver, fareText);

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", DriverDetailsEvent,
                "Your RideReady driver", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideReady driver", message));

            // With the driver-details template configured each detail is its own variable, so WhatsApp shows them on separate lines.
            if (!string.IsNullOrWhiteSpace(_whatsAppSettings.DriverDetailsTemplateName))
            {
                var fields = DriverDetailsFields(booking, driver, quote?.TotalEstimatedFare);
                return await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", DriverDetailsEvent,
                    null, message,
                    () => _whatsAppSender.SendTemplateAsync(booking.Customer.Phone, _whatsAppSettings.DriverDetailsTemplateName, fields));
            }

            // Otherwise it goes as one line inside the general template, which already begins "Update on your RideReady booking:".
            var oneLine = string.IsNullOrWhiteSpace(_whatsAppSettings.TemplateName)
                ? message
                : message.Replace($"Your RideReady booking {booking.BookingReference} is confirmed.", $"{booking.BookingReference} is confirmed.");
            return await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", DriverDetailsEvent,
                null, oneLine,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, oneLine));
        }

        public async Task SendBookingCompletedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Thanks for riding with RideReady! Your trip {booking.BookingReference} is complete.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "BookingCompleted",
                "Trip complete", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Trip complete", message));

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "BookingCompleted",
                null, message,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, message));

            var operatorMessage = WithLink($"Booking {booking.BookingReference} completed.", "Open", _magicLinks?.LinkForAdmin(booking));
            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "BookingCompleted",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }

        public async Task SendBookingCancelledNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} has been cancelled.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "BookingCancelled",
                "Booking cancelled", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Booking cancelled", message));

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
                    await SendAndLogAsync(bookingId, "Driver", driver.Id, driver.Phone, "WhatsApp", "BookingCancelled",
                        null, driverMessage,
                        () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));
                }
            }
        }

        public async Task SendUnassignedReminderAsync(int bookingId, bool urgent)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var eventType = urgent ? "Escalation_30min" : "Reminder_1hr";
            var message = urgent
                ? $"URGENT: booking {booking.BookingReference} has no driver assigned and pickup is in 30 minutes."
                : $"Reminder: booking {booking.BookingReference} has no driver assigned and pickup is in 1 hour.";
            message = WithLink(message, "Open", _magicLinks?.LinkForAdmin(booking));

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", eventType,
                eventType, message,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, eventType, message));
        }

        public async Task SendNoShowNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} was marked as a no-show because pickup wasn't confirmed 30 minutes after the scheduled time.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "NoShow",
                "Booking marked as no-show", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Booking marked as no-show", message));
        }

        private async Task<bool> SendAndLogAsync(
            int bookingId, string recipientType, int? recipientId, string recipientContact, string channel,
            string eventType, string? subject, string messageContent, Func<Task> send)
        {
            var notification = new Notification
            {
                BookingId = bookingId,
                RecipientType = recipientType,
                RecipientId = recipientId,
                RecipientContact = recipientContact,
                Channel = channel,
                EventType = eventType,
                Subject = subject,
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
                notification.LastAttemptAt = DateTime.UtcNow;
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

            return notification.DeliveryStatus == "Sent";
        }
    }
}
