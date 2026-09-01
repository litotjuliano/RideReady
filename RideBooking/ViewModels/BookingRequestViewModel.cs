using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class BookingRequestViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 3)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60|0)[0-9]{9,10}$",
            ErrorMessage = "Invalid Malaysian phone number")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup location is required")]
        [StringLength(255, MinimumLength = 5)]
        public string PickupLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Destination is required")]
        [StringLength(255, MinimumLength = 5)]
        public string Destination { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup date is required")]
        public DateOnly PickupDate { get; set; }

        [Required(ErrorMessage = "Pickup time is required")]
        public TimeOnly PickupTime { get; set; }

        [Required(ErrorMessage = "Passengers count is required")]
        [Range(1, 8)]
        public int Passengers { get; set; }

        [Required(ErrorMessage = "Bags count is required")]
        [Range(0, 10)]
        public int Bags { get; set; }

        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
