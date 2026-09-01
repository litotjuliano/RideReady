using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class UpdateStatusViewModel
    {
        [Required]
        public int BookingId { get; set; }

        [Required(ErrorMessage = "A new status is required")]
        public string NewStatus { get; set; } = string.Empty;
    }
}
