namespace RideReady.ViewModels
{
    public enum StepState { Done, Current, Upcoming }

    public record ProgressStep(int Number, string Label, StepState State);

    /// <summary>
    /// The booking lifecycle shown as numbered progress circles on the admin dashboard. State is derived
    /// purely from the booking's status, so nothing extra is stored.
    /// </summary>
    public static class BookingProgress
    {
        private static readonly (string Status, string Label)[] Sequence =
        {
            ("New", "New"),
            ("Driver_Assigned", "Assigned"),
            ("Confirmed", "Confirmed"),
            ("Picked_Up", "Picked up"),
            ("In_Transit", "In transit"),
            ("Dropped_Off", "Dropped off"),
            ("Completed", "Completed")
        };

        public static int TotalSteps => Sequence.Length;

        /// <summary>The label to show instead of the steps for Cancelled / No_Show, which sit outside the sequence; otherwise null.</summary>
        public static string? OffTrackLabel(string? status) => status switch
        {
            "Cancelled" => "Cancelled",
            "No_Show" => "No-show",
            _ => null
        };

        /// <summary>
        /// Steps before the current status are Done, the current one is Current, the rest Upcoming. A Completed
        /// booking has every step Done. An unknown or off-track status lights nothing.
        /// </summary>
        public static IReadOnlyList<ProgressStep> GetSteps(string? status)
        {
            var currentIndex = Array.FindIndex(Sequence, s => s.Status == status);
            var completed = status == "Completed";

            return Sequence
                .Select((s, i) => new ProgressStep(
                    i + 1,
                    s.Label,
                    currentIndex < 0 ? StepState.Upcoming
                        : completed || i < currentIndex ? StepState.Done
                        : i == currentIndex ? StepState.Current
                        : StepState.Upcoming))
                .ToList();
        }
    }
}
