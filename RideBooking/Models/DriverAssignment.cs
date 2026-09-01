namespace RideBooking.Models
{
    public class DriverAssignment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string AssignmentStatus { get; set; } = "Pending"; // Pending, Accepted, Rejected
    }
}
