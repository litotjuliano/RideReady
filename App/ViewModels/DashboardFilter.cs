namespace RideReady.ViewModels
{
    /// <summary>What the admin dashboard's search box and status chips work from.</summary>
    public static class DashboardFilter
    {
        public const string New = "new";
        public const string Assigned = "assigned";
        public const string Confirmed = "confirmed";
        public const string OnTrip = "ontrip";

        /// <summary>The chips, in order: key and label.</summary>
        public static readonly IReadOnlyList<(string Key, string Label)> StatusChips = new[]
        {
            (New, "New"),
            (Assigned, "Assigned"),
            (Confirmed, "Confirmed"),
            (OnTrip, "On trip")
        };

        /// <summary>Which chip a booking status belongs to. Statuses without a chip return "other".</summary>
        public static string StatusGroup(string status) => status switch
        {
            "New" => New,
            "Driver_Assigned" => Assigned,
            "Confirmed" => Confirmed,
            "Picked_Up" or "In_Transit" or "Dropped_Off" => OnTrip,
            _ => "other"
        };

        /// <summary>
        /// The lower-case text the search box matches against: customer, phone, reference, route, addresses and
        /// the assigned driver. Digits-only phone variants are included so "0123456789" finds "012-345 6789".
        /// </summary>
        public static string SearchText(AdminBookingListItemViewModel booking)
        {
            var parts = new[]
            {
                booking.CustomerName,
                booking.CustomerPhone,
                PhoneLinks.ToInternational(booking.CustomerPhone),
                booking.CustomerPhone is { } phone ? new string(phone.Where(char.IsDigit).ToArray()) : null,
                booking.BookingReference,
                booking.FromName,
                booking.ToName,
                booking.PickupLocation,
                booking.Destination,
                booking.AssignedDriverName
            };

            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
        }
    }
}
