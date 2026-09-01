using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    public class DriverAuthController : Controller
    {
        private readonly IDriverPortalService _driverPortalService;

        public DriverAuthController(IDriverPortalService driverPortalService)
        {
            _driverPortalService = driverPortalService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(DriverLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var driver = await _driverPortalService.AuthenticateAsync(model.Phone, model.Pin);
            if (driver == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid phone number or PIN");
                return View(model);
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, driver.Id.ToString()),
                    new Claim(ClaimTypes.Name, driver.Name)
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync("DriverAuth", new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Driver");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("DriverAuth");
            return RedirectToAction(nameof(Login));
        }
    }
}
