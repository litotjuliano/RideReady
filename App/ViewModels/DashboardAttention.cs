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

        // ---- every flag the dashboard can raise, in priority order (also the order of the summary chips) ----

        public sealed record AttentionFlag(string Key, string Label);

        public static readonly AttentionFlag OverdueFlag = new("pickup-overdue", PickupOverdue);
        public static readonly AttentionFlag SoonFlag = new("pickup-soon", PickupSoon);
        public static readonly AttentionFlag NeedsDriverFlag = new("needs-driver", "Assign a driver");
        public static readonly AttentionFlag WaitingFlag = new("waiting-driver", "Waiting for driver");
        public static readonly AttentionFlag SendDetailsFlag = new("send-details", "Send details to customer");

        public static IReadOnlyList<AttentionFlag> AllFlags { get; } =
            new[] { OverdueFlag, SoonFlag, NeedsDriverFlag, WaitingFlag, SendDetailsFlag };

        /// <summary>
        /// Everything the dispatcher should act on for one booking, most urgent first:
        /// the pickup time (soon or overdue), a booking still without a driver (or whose driver declined), a driver
        /// who has not answered yet, and an accepted trip whose driver details have not been sent to the customer.
        /// </summary>
        public static IReadOnlyList<AttentionFlag> FlagsFor(
            string status, string? assignmentStatus, bool detailsSentToCustomer,
            DateOnly pickupDate, TimeOnly pickupTime, DateTime utcNow)
        {
            var flags = new List<AttentionFlag>();

            switch (PickupAlert(status, pickupDate, pickupTime, utcNow))
            {
                case PickupOverdue: flags.Add(OverdueFlag); break;
                case PickupSoon: flags.Add(SoonFlag); break;
            }

            if (status == "New" && (assignmentStatus == null || assignmentStatus == "Rejected"))
            {
                flags.Add(NeedsDriverFlag);
            }

            if (status == "Driver_Assigned" && assignmentStatus == "Pending")
            {
                flags.Add(WaitingFlag);
            }

            if (status == "Confirmed" && assignmentStatus == "Accepted" && !detailsSentToCustomer)
            {
                flags.Add(SendDetailsFlag);
            }

            return flags;
        }
    }
}
