namespace RideReady.ViewModels
{
    /// <summary>
    /// How many passengers each vehicle type can carry, and the friendly message when a booking asks for more.
    /// The per-type limit comes from the drivers: it is the largest passenger capacity among the ACTIVE drivers of
    /// that type. When no driver of a type has a capacity recorded, the default below is used. The booking page
    /// (hints and live check), the booking controller and the booking service all use this one class.
    /// </summary>
    public static class VehicleCapacity
    {
        /// <summary>No single booking may ask for more passengers than this, whatever the vehicle.</summary>
        public const int HardLimit = 100;

        // Used for a vehicle type until at least one active driver of that type has a capacity recorded.
        private static readonly (string Type, int Default)[] Defaults =
        {
            ("Car", 4),
            ("Van", 8),
            ("Bus", 8)
        };

        /// <summary>The vehicle types in the order they are offered.</summary>
        public static IReadOnlyList<string> Types => Defaults.Select(d => d.Type).ToList();

        /// <summary>The default limit per vehicle type (used when drivers have no capacity recorded).</summary>
        public static IReadOnlyDictionary<string, int> DefaultCapacities() =>
            Defaults.ToDictionary(d => d.Type, d => d.Default);

        /// <summary>
        /// The limit per vehicle type: the largest capacity among active drivers of that type that have one,
        /// otherwise the default for that type.
        /// </summary>
        public static IReadOnlyDictionary<string, int> FromDrivers(
            IEnumerable<(string VehicleType, int? PassengerCapacity, bool IsActive)> drivers)
        {
            var list = drivers.ToList();
            var result = new Dictionary<string, int>();

            foreach (var (type, defaultMax) in Defaults)
            {
                var recorded = list
                    .Where(d => d.IsActive && d.VehicleType == type && d.PassengerCapacity is > 0)
                    .Select(d => d.PassengerCapacity!.Value)
                    .ToList();

                result[type] = recorded.Count > 0 ? recorded.Max() : defaultMax;
            }

            return result;
        }

        /// <summary>The limit for a vehicle type, or null when the type is unknown.</summary>
        public static int? MaxFor(IReadOnlyDictionary<string, int> capacities, string? vehicleType) =>
            vehicleType != null && capacities.TryGetValue(vehicleType, out var max) ? max : null;

        /// <summary>
        /// Null when the passenger count fits the vehicle (or the vehicle is unknown). Otherwise a short, friendly
        /// message that gives the limit and points to a bigger vehicle type.
        /// </summary>
        public static string? ExceededMessage(IReadOnlyDictionary<string, int> capacities, string? vehicleType, int passengers)
        {
            if (MaxFor(capacities, vehicleType) is not int max || passengers <= max)
            {
                return null;
            }

            var bigger = Types.Where(t => capacities.TryGetValue(t, out var m) && m >= passengers).ToList();
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
