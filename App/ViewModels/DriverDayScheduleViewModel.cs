namespace RideReady.ViewModels
{
    public class DriverDayScheduleViewModel
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public bool IsOnTimeOff { get; set; }
        public string? TimeOffReason { get; set; }
        public List<DriverScheduleBlockViewModel> Blocks { get; set; } = new();
    }
}
