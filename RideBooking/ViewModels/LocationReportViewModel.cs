namespace RideBooking.ViewModels
{
    public class LocationReportViewModel
    {
        public int? BookingId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int? AccuracyMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
    }
}
