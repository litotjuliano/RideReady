using System.ComponentModel.DataAnnotations;

namespace RideReady.ViewModels
{
    public class BookingRequestViewModel : IValidatableObject
    {
        /// <summary>The starting values of an empty booking form: today's date, a Car and one passenger.</summary>
        public static BookingRequestViewModel NewForm(DateOnly today) => new()
        {
            PickupDate = today,
            DropOffDate = today,
            VehicleType = "Car",
            Passengers = 1
        };

        [Display(Name = "Full name")]
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Please enter your full name, between 3 and 100 characters.")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "Phone")]
        [Required(ErrorMessage = "Please enter your phone number.")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Enter a Malaysian phone number, like 012-3456789.")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a full email address, like you@example.com.")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Display(Name = "From")]
        [Required(ErrorMessage = "Please enter where you are starting from.")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Please use between 2 and 255 characters.")]
        public string? FromName { get; set; }

        [Display(Name = "To")]
        [Required(ErrorMessage = "Please enter where you are going to.")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Please use between 2 and 255 characters.")]
        public string? ToName { get; set; }

        // Stored in Booking.PickupLocation.
        [Display(Name = "Pick up address")]
        [Required(ErrorMessage = "Please enter the pick up address.")]
        [StringLength(255, MinimumLength = 5, ErrorMessage = "Please use between 5 and 255 characters.")]
        public string? PickupLocation { get; set; }

        // Stored in Booking.Destination.
        [Display(Name = "Drop-off address")]
        [Required(ErrorMessage = "Please enter the drop-off address.")]
        [StringLength(255, MinimumLength = 5, ErrorMessage = "Please use between 5 and 255 characters.")]
        public string Destination { get; set; } = string.Empty;

        [Display(Name = "Pickup date")]
        [Required(ErrorMessage = "Please choose a pickup date.")]
        public DateOnly PickupDate { get; set; }

        [Display(Name = "Pickup time")]
        [Required(ErrorMessage = "Please choose a pickup time.")]
        public TimeOnly PickupTime { get; set; }

        [Display(Name = "Drop-off date")]
        [Required(ErrorMessage = "Please choose a drop-off date.")]
        public DateOnly? DropOffDate { get; set; }

        [Display(Name = "Drop-off time (approximate)")]
        [Required(ErrorMessage = "Please choose an approximate drop-off time.")]
        public TimeOnly? DropOffTime { get; set; }

        [Display(Name = "Passengers")]
        [Required(ErrorMessage = "Please enter how many passengers.")]
        [Range(1, VehicleCapacity.HardLimit, ErrorMessage = "Please enter between 1 and 100 passengers.")]
        public int Passengers { get; set; }

        [Display(Name = "Bags")]
        [Required(ErrorMessage = "Please enter how many bags (0 if none).")]
        [Range(0, 10, ErrorMessage = "Please enter between 0 and 10 bags.")]
        public int Bags { get; set; }

        [Display(Name = "Vehicle type")]
        [Required(ErrorMessage = "Please choose a vehicle type.")]
        [RegularExpression("^(Car|Van|Bus|Others)$", ErrorMessage = "Please choose Car, Van, Bus or Others.")]
        public string VehicleType { get; set; } = string.Empty;

        /// <summary>Only used when the vehicle type is "Others": what vehicle the customer needs, in their own words.</summary>
        [Display(Name = "Vehicle needed")]
        [StringLength(100, ErrorMessage = "Please keep the vehicle description under 100 characters.")]
        public string? OtherVehicle { get; set; }

        [Display(Name = "Remarks")]
        [StringLength(500, ErrorMessage = "Please keep remarks under 500 characters.")]
        public string? Notes { get; set; }

        [Display(Name = "Payment method")]
        [Required(ErrorMessage = "Please choose a payment method.")]
        [RegularExpression("^(Pay_at_Pickup|Bank_Transfer)$", ErrorMessage = "Please choose Pay at Pickup or Bank Transfer.")]
        public string PaymentMethod { get; set; } = "Pay_at_Pickup";

        [MustBeTrue(ErrorMessage = "Please accept the terms and conditions to continue.")]
        public bool AcceptedTerms { get; set; }

        /// <summary>The approximate drop-off must come after the pickup, and "Others" needs a description.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (VehicleType == VehicleCapacity.Others && string.IsNullOrWhiteSpace(OtherVehicle))
            {
                yield return new ValidationResult(
                    "Please tell us what vehicle you need.",
                    new[] { nameof(OtherVehicle) });
            }

            if (DropOffDate is DateOnly dropOffDate && DropOffTime is TimeOnly dropOffTime
                && dropOffDate.ToDateTime(dropOffTime) <= PickupDate.ToDateTime(PickupTime))
            {
                yield return new ValidationResult(
                    "The drop-off time must be after the pickup time.",
                    new[] { nameof(DropOffTime) });
            }
        }
    }
}
