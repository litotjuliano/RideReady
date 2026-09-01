using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    public class AdminAuthController : Controller
    {
        private readonly AdminCredentialsSettings _credentials;

        public AdminAuthController(IOptions<AdminCredentialsSettings> credentials)
        {
            _credentials = credentials.Value;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Username != _credentials.Username || model.Password != _credentials.Password)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View(model);
            }

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, model.Username) },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync("AdminAuth", new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminAuth");
            return RedirectToAction(nameof(Login));
        }
    }
}
