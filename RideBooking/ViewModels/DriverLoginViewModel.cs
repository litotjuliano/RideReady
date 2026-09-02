using System.ComponentModel.DataAnnotations;

namespace RideReady.ViewModels
{
    public class DriverLoginViewModel
    {
        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Pin { get; set; } = string.Empty;
    }
}
