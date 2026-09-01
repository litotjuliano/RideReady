using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class CreateDriverViewModel
    {
        [Required(ErrorMessage = "Driver name is required")]
        [StringLength(255, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Invalid Malaysian phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle number is required")]
        [StringLength(50)]
        public string VehicleNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "A 4-6 digit PIN is required for driver portal login")]
        [RegularExpression(@"^\d{4,6}$", ErrorMessage = "PIN must be 4-6 digits")]
        public string Pin { get; set; } = string.Empty;
    }
}
