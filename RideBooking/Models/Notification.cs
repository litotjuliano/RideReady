namespace RideBooking.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string RecipientType { get; set; } = string.Empty; // Customer, Driver, Operator
        public int? RecipientId { get; set; }
        public string RecipientContact { get; set; } = string.Empty; // email address or phone number, used for retries
        public string Channel { get; set; } = string.Empty; // Email, WhatsApp, SMS, Push, Calendar
        public string EventType { get; set; } = string.Empty;
        public string? Subject { get; set; } // used for Email retries
        public string? MessageContent { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Failed, DeadLetter
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
