using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class SetFareViewModel
    {
        [Required]
        public int BookingId { get; set; }

        [Required(ErrorMessage = "Enter a fare")]
        [Range(0.01, 100000, ErrorMessage = "Fare must be greater than zero")]
        public decimal Fare { get; set; }
    }
}
