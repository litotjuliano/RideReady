using System.ComponentModel.DataAnnotations;

namespace RideReady.ViewModels
{
    public class AddTimeOffViewModel
    {
        [Required]
        public int DriverId { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateOnly EndDate { get; set; }

        [StringLength(255)]
        public string? Reason { get; set; }
    }
}
