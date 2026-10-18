namespace RideReady.ViewModels
{
    public class AdminBookingListItemViewModel
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string? Notes { get; set; }
        public string? PaymentMethod { get; set; }

        /// <summary>"Pickup soon" / "Pickup overdue" when the dispatcher needs to act on the pickup time; otherwise null.</summary>
        public string? PickupAlert { get; set; }

        /// <summary>Everything the dispatcher should act on for this booking, most urgent first (see DashboardAttention).</summary>
        public IReadOnlyList<DashboardAttention.AttentionFlag> AttentionFlags { get; set; } = Array.Empty<DashboardAttention.AttentionFlag>();
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string? FromName { get; set; }
        public string? ToName { get; set; }
        public DateOnly? DropOffDate { get; set; }
        public TimeOnly? DropOffTime { get; set; }
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }

        /// <summary>When the booking was made, in Malaysia time (UTC+8, no DST) for display.</summary>
        public DateTime BookedAt { get; set; }

        /// <summary>When the driver details were last sent to the customer via WhatsApp (Malaysia time), or null if not yet sent.</summary>
        public DateTime? DriverDetailsSentAt { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string RequestedVehicleType { get; set; } = string.Empty;
        public string? OtherVehicle { get; set; }
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
