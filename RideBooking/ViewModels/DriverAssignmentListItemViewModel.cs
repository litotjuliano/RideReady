namespace RideBooking.ViewModels
{
    public class DriverAssignmentListItemViewModel
    {
        public int AssignmentId { get; set; }
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string? Notes { get; set; }
        public string AssignmentStatus { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
    }
}
