namespace RideReady.ViewModels
{
    /// <summary>
    /// The single source of truth for how many passengers each vehicle type carries. The booking page (hints and
    /// the live check), the form validation and the booking service all read from here, so changing a number
    /// changes it everywhere.
    /// </summary>
    public static class VehicleCapacity
    {
        /// <summary>The booking form never accepts more passengers than this, whatever the vehicle.</summary>
        public const int Largest = 8;

        // Car is 4. Van and Bus are set to the current overall limit of 8 until the real capacities are confirmed,
        // so they are no stricter than before.
        private static readonly (string Type, int Max)[] Table =
        {
            ("Car", 4),
            ("Van", 8),
            ("Bus", 8)
        };

        /// <summary>The vehicle types in the order they are offered.</summary>
        public static IReadOnlyList<string> Types => Table.Select(t => t.Type).ToList();

        /// <summary>The maximum passengers for a vehicle type, or null when the type is unknown.</summary>
        public static int? MaxFor(string? vehicleType)
        {
            foreach (var (type, max) in Table)
            {
                if (type == vehicleType) return max;
            }
            return null;
        }

        /// <summary>
        /// Null when the passenger count fits the vehicle (or the vehicle is unknown). Otherwise a short, friendly
        /// message that says the limit and points to a bigger vehicle.
        /// </summary>
        public static string? ExceededMessage(string? vehicleType, int passengers)
        {
            if (MaxFor(vehicleType) is not int max || passengers <= max)
            {
                return null;
            }

            var bigger = Table.Where(t => t.Max >= passengers).Select(t => t.Type).ToList();
            var advice = bigger.Count switch
            {
                0 => "Please contact us for a larger group.",
                1 => $"Choose a {bigger[0]}.",
                _ => $"Choose a {string.Join(" or ", bigger)}."
            };

            return $"A {vehicleType} carries up to {max} passengers. {advice}";
        }
    }
}
