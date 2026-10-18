using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Services;

namespace RideReady.Controllers
{
    /// <summary>
    /// Opens a signed link from a WhatsApp or email message: signs the admin or the driver in (same cookies as the normal
    /// login) and sends them to the right page. Expired, changed or unknown links show a friendly page instead.
    /// </summary>
    public class MagicLinkController : Controller
    {
        private readonly IMagicLinkService _links;
        private readonly RideReadyDbContext _context;
        private readonly AdminCredentialsSettings _adminCredentials;

        public MagicLinkController(IMagicLinkService links, RideReadyDbContext context, IOptions<AdminCredentialsSettings> adminCredentials)
        {
            _links = links;
            _context = context;
            _adminCredentials = adminCredentials.Value;
        }

        [HttpGet("l/{token}")]
        public async Task<IActionResult> Open(string token)
        {
            // The link is a key: keep it out of caches and out of the Referer header sent to other sites.
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";

            var result = _links.Read(token);
            if (result.Status != MagicLinkStatus.Valid || result.Link is null)
            {
                return ExpiredPage(result.Status);
            }

            var link = result.Link;
            if (link.Role == MagicLinkRole.Admin)
            {
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, _adminCredentials.Username) },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync("AdminAuth", new ClaimsPrincipal(identity));
                return RedirectToAction("Index", "Admin", new { focus = link.BookingId });
            }

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == link.DriverId);
            if (driver is null || !driver.IsActive)
            {
                return ExpiredPage(MagicLinkStatus.Invalid, isDriver: true);
            }

            var driverIdentity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, driver.Id.ToString()),
                    new Claim(ClaimTypes.Name, driver.Name)
                },
                CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync("DriverAuth", new ClaimsPrincipal(driverIdentity));
            return RedirectToAction("Index", "Driver");
        }

        private IActionResult ExpiredPage(MagicLinkStatus status, bool isDriver = false)
        {
            ViewBag.Expired = status == MagicLinkStatus.Expired;
            ViewBag.LoginController = isDriver ? "DriverAuth" : "AdminAuth";
            Response.StatusCode = StatusCodes.Status410Gone;
            return View("Expired");
        }
    }
}
