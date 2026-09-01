namespace RideBooking.Models
{
    public class DriverLocation
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public int? BookingId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int? AccuracyMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
