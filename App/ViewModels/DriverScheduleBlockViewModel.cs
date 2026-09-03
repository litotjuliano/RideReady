namespace RideReady.ViewModels
{
    public class DriverScheduleBlockViewModel
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public TimeOnly Start { get; set; }
        public TimeOnly End { get; set; }
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
    }
}
