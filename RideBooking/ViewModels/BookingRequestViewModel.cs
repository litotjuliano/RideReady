using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class BookingRequestViewModel
    {
        [Display(Name = "Full name")]
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 3)]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Phone")]
        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Invalid Malaysian phone number. Use +60XXXXXXXXX or 01X-XXXXXXXX format")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Display(Name = "Pickup location")]
        [Required(ErrorMessage = "Pickup location is required")]
        [StringLength(255, MinimumLength = 5)]
        public string PickupLocation { get; set; } = string.Empty;

        [Display(Name = "Destination")]
        [Required(ErrorMessage = "Destination is required")]
        [StringLength(255, MinimumLength = 5)]
        public string Destination { get; set; } = string.Empty;

        [Display(Name = "Pickup date")]
        [Required(ErrorMessage = "Pickup date is required")]
        public DateOnly PickupDate { get; set; }

        [Display(Name = "Pickup time")]
        [Required(ErrorMessage = "Pickup time is required")]
        public TimeOnly PickupTime { get; set; }

        [Display(Name = "Passengers")]
        [Required(ErrorMessage = "Passengers count is required")]
        [Range(1, 8)]
        public int Passengers { get; set; }

        [Display(Name = "Bags")]
        [Required(ErrorMessage = "Bags count is required")]
        [Range(0, 10)]
        public int Bags { get; set; }

        [Display(Name = "Vehicle type")]
        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Payment method")]
        [Required(ErrorMessage = "Payment method is required")]
        [RegularExpression("^(Pay_at_Pickup|Bank_Transfer)$")]
        public string PaymentMethod { get; set; } = "Pay_at_Pickup";

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions")]
        public bool AcceptedTerms { get; set; }
    }
}
