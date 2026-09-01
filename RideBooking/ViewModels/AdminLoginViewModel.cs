using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class AdminLoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
