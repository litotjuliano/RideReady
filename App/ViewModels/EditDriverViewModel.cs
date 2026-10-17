using System.ComponentModel.DataAnnotations;

namespace RideReady.ViewModels
{
    /// <summary>Details of a driver that the admin can change. The PIN is deliberately not editable here.</summary>
    public class EditDriverViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Driver name")]
        [Required(ErrorMessage = "Driver name is required")]
        [StringLength(255, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Phone")]
        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Invalid Malaysian phone number")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Vehicle type")]
        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [Display(Name = "Plate no.")]
        [Required(ErrorMessage = "Vehicle number is required")]
        [StringLength(50)]
        public string VehicleNumber { get; set; } = string.Empty;

        [Display(Name = "Passenger capacity")]
        [Required(ErrorMessage = "Enter how many passengers the vehicle carries")]
        [Range(1, VehicleCapacity.HardLimit, ErrorMessage = "Passenger capacity must be between 1 and 100")]
        public int? PassengerCapacity { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
