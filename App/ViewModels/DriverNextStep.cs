namespace RideReady.ViewModels
{
    /// <summary>The one button a driver normally taps next: the status that follows the trip's current one.</summary>
    public static class DriverNextStep
    {
        public sealed record Step(string NewStatus, string ButtonLabel);

        /// <summary>The next step for an accepted trip, or null when there is none (not started by the driver, or finished).</summary>
        public static Step? For(string bookingStatus) => bookingStatus switch
        {
            "Confirmed" => new Step("Picked_Up", "Picked up"),
            "Picked_Up" => new Step("In_Transit", "Start trip"),
            "In_Transit" => new Step("Dropped_Off", "Dropped off"),
            "Dropped_Off" => new Step("Completed", "Complete trip"),
            _ => null
        };
    }
}
