namespace RideBooking.ViewModels
{
    public class BookingQuoteViewModel
    {
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
        public string EstimatedDuration { get; set; } = string.Empty;
        public List<string> PaymentMethods { get; set; } = new();
    }
}
