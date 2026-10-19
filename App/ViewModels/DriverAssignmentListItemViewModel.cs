namespace RideReady.ViewModels
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
        public string? FromName { get; set; }
        public string? ToName { get; set; }
        public DateOnly? DropOffDate { get; set; }
        public TimeOnly? DropOffTime { get; set; }
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string? Notes { get; set; }
        public string? RequestedVehicleType { get; set; }
        public string? OtherVehicle { get; set; }
        public string AssignmentStatus { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
    }
}
