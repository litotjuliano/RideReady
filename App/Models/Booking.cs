namespace RideReady.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string RequestedVehicleType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Status { get; set; } = "New"; // New, Confirmed, Driver_Assigned, Picked_Up, In_Transit, Dropped_Off, Completed, Cancelled, No_Show
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DriverAssignment? CurrentAssignment { get; set; }
        public BookingQuote? Quote { get; set; }
        public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
