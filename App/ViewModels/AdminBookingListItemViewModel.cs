namespace RideReady.ViewModels
{
    public class AdminBookingListItemViewModel
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }

        /// <summary>When the booking was made, in Malaysia time (UTC+8, no DST) for display.</summary>
        public DateTime BookedAt { get; set; }

        /// <summary>When the driver details were last sent to the customer via WhatsApp (Malaysia time), or null if not yet sent.</summary>
        public DateTime? DriverDetailsSentAt { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string RequestedVehicleType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? EstimatedFare { get; set; }
        public int? AssignedDriverId { get; set; }
        public string? AssignedDriverName { get; set; }
        public string? AssignedDriverPhone { get; set; }
        public string? AssignmentStatus { get; set; }

        // Populated by AdminController.Index — key is a driver id, value is why that
        // driver isn't free at this booking's pickup time (e.g. "busy: RR-... 09:00-11:00"
        // or "on time off"). Absence from the dictionary means no known conflict.
        public Dictionary<int, string> DriverConflicts { get; set; } = new();
    }
}
