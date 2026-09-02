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
        private readonly INotificationService _notificationService;
        private readonly IBookingService _bookingService;

        public AdminController(
            IDriverAssignmentService driverAssignmentService,
            INotificationService notificationService,
            IBookingService bookingService)
        {
            _driverAssignmentService = driverAssignmentService;
            _notificationService = notificationService;
            _bookingService = bookingService;
        }

        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            ViewBag.ActiveDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            return View(bookings);
        }

        public async Task<IActionResult> Drivers()
        {
            var drivers = await _driverAssignmentService.GetAllDriversAsync();
            return View(drivers);
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
                await _notificationService.SendDriverAssignedNotificationAsync(model.BookingId, model.DriverId);
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
                return RedirectToAction(nameof(Drivers));
            }

            await _driverAssignmentService.CreateDriverAsync(model);
            TempData["SuccessMessage"] = "Driver added.";
            return RedirectToAction(nameof(Drivers));
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

                if (model.NewStatus == "Cancelled")
                {
                    await _notificationService.SendBookingCancelledNotificationAsync(model.BookingId);
                }
                else if (model.NewStatus == "Completed")
                {
                    await _notificationService.SendBookingCompletedNotificationAsync(model.BookingId);
                }

                TempData["SuccessMessage"] = "Status updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFare(SetFareViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Enter a valid fare greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _bookingService.SetManualFareAsync(model.BookingId, model.Fare);
                TempData["SuccessMessage"] = "Fare saved.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
