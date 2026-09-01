using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class AssignDriverViewModel
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public int DriverId { get; set; }
    }
}
