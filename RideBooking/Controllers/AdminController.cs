using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AdminController : Controller
    {
        private readonly IDriverAssignmentService _driverAssignmentService;

        public AdminController(IDriverAssignmentService driverAssignmentService)
        {
            _driverAssignmentService = driverAssignmentService;
        }

        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            ViewBag.ActiveDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDriver(AssignDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.AssignDriverAsync(model.BookingId, model.DriverId);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver(CreateDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.CreateDriverAsync(model);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            await _driverAssignmentService.UpdateBookingStatusAsync(bookingId, newStatus, User.Identity?.Name ?? "Admin");
            return RedirectToAction(nameof(Index));
        }
    }
}
