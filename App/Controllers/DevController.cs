using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Data;

namespace RideReady.Controllers
{
    /// <summary>
    /// Local testing only: lists the latest messages the system sent (or would have sent), so the sign-in links in them can be
    /// tapped on a phone while WhatsApp is switched off. It answers 404 everywhere except the Development environment.
    /// </summary>
    public class DevController : Controller
    {
        private readonly RideReadyDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DevController(RideReadyDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("dev/outbox")]
        public async Task<IActionResult> Outbox()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            var latest = await _context.Notifications
                .Where(n => n.MessageContent != null && (n.RecipientType == "Operator" || n.RecipientType == "Driver"))
                .OrderByDescending(n => n.Id)
                .Take(30)
                .ToListAsync();
            return View(latest);
        }
    }
}
