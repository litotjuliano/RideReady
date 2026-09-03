namespace RideReady.Models
{
    public class DriverTimeOff
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
