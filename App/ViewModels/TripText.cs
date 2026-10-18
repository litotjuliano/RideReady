namespace RideReady.ViewModels
{
    /// <summary>
    /// Shared wording for a booking's route. A short place name (From / To) wins when the customer gave one;
    /// otherwise the full address is shown.
    /// </summary>
    public static class TripText
    {
        /// <summary>The short name if given, else the address, else "-".</summary>
        public static string Place(string? shortName, string? address)
        {
            if (!string.IsNullOrWhiteSpace(shortName)) return shortName.Trim();
            if (!string.IsNullOrWhiteSpace(address)) return address.Trim();
            return "-";
        }

        /// <summary>"From -> To". Pass a different arrow (for example a real arrow for screens) when needed.</summary>
        public static string Route(string? from, string? pickupAddress, string? to, string? dropOffAddress, string arrow = "->") =>
            $"{Place(from, pickupAddress)} {arrow} {Place(to, dropOffAddress)}";

        /// <summary>The vehicle for display: "Car", or "Others: lorry" when the customer described something else.</summary>
        public static string Vehicle(string? vehicleType, string? otherDescription)
        {
            var type = string.IsNullOrWhiteSpace(vehicleType) ? string.Empty : vehicleType.Trim();
            if (type == VehicleCapacity.Others)
            {
                return string.IsNullOrWhiteSpace(otherDescription) ? type : $"{type}: {otherDescription.Trim()}";
            }
            return type;
        }

        /// <summary>Customer-facing wording for how the trip is paid.</summary>
        public static string PaymentMethod(string? paymentMethod) =>
            paymentMethod == "Bank_Transfer" ? "Bank Transfer" : "Pay at Pickup";

        /// <summary>"yyyy-MM-dd HH:mm" when both parts are present, otherwise an empty string.</summary>
        public static string DropOffAt(DateOnly? date, TimeOnly? time) =>
            date is DateOnly d && time is TimeOnly t ? $"{d:yyyy-MM-dd} {t:HH:mm}" : string.Empty;
    }
}
