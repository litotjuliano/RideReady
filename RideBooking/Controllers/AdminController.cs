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
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Choose a booking and a driver before assigning.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _driverAssignmentService.AssignDriverAsync(model.BookingId, model.DriverId);
                TempData["SuccessMessage"] = "Driver assigned.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver(CreateDriverViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Could not add driver — check the details and try again.";
                return RedirectToAction(nameof(Index));
            }

            await _driverAssignmentService.CreateDriverAsync(model);
            TempData["SuccessMessage"] = "Driver added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(UpdateStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Choose a booking and a status before updating.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _driverAssignmentService.UpdateBookingStatusAsync(
                    model.BookingId, model.NewStatus, User?.Identity?.Name ?? "Admin");
                TempData["SuccessMessage"] = "Status updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
