namespace RideReady.Models
{
    public class BookingQuote
    {
        public int Id { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public decimal BaseFare { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal DistanceCharge { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TimeCharge { get; set; }
        public decimal PassengerSurcharge { get; set; }
        public decimal LuggageFee { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ServiceTax { get; set; }
        public decimal TotalEstimatedFare { get; set; }
        public decimal? ActualFare { get; set; }
        public string PaymentMethod { get; set; } = string.Empty; // Pay_at_Pickup, Bank_Transfer
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
