namespace RideBooking.Models
{
    public class PricingSetting
    {
        public int Id { get; set; }
        public string VehicleType { get; set; } = string.Empty; // Car, Van, Bus
        public decimal BaseFare { get; set; }
        public decimal PerKmRate { get; set; }
        public decimal PerHourRate { get; set; }
        public int FirstKmDistance { get; set; }
        public decimal? FirstKmCharge { get; set; }
        public decimal? PassengerSurcharge { get; set; }
        public decimal ServiceTaxPercent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
