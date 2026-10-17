namespace RideReady.ViewModels
{
    /// <summary>
    /// Flags on the admin dashboard that tell the dispatcher a booking needs action. Pure functions with an
    /// injectable clock so they are easy to test. Times are Malaysia time (UTC+8, no daylight saving).
    /// </summary>
    public static class DashboardAttention
    {
        public const string PickupSoon = "Pickup soon";
        public const string PickupOverdue = "Pickup overdue";

        /// <summary>A pickup counts as "soon" when it is at most this far away.</summary>
        public static readonly TimeSpan PickupSoonWindow = TimeSpan.FromHours(2);

        // Once the passenger has been picked up (or the booking is closed) a pickup alert makes no sense.
        private static readonly string[] NotYetPickedUp = { "New", "Driver_Assigned", "Confirmed" };

        private const int MalaysiaOffsetHours = 8;

        /// <summary>
        /// "Pickup soon" when the pickup is within 2 hours, "Pickup overdue" when the pickup time has already
        /// passed and the passenger has not been picked up, otherwise null.
        /// </summary>
        public static string? PickupAlert(string status, DateOnly pickupDate, TimeOnly pickupTime, DateTime utcNow)
        {
            if (!NotYetPickedUp.Contains(status))
            {
                return null;
            }

            var nowInMalaysia = utcNow.AddHours(MalaysiaOffsetHours);
            var untilPickup = pickupDate.ToDateTime(pickupTime) - nowInMalaysia;

            if (untilPickup < TimeSpan.Zero) return PickupOverdue;
            if (untilPickup <= PickupSoonWindow) return PickupSoon;
            return null;
        }
    }
}
