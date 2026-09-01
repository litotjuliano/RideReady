using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    [Authorize(AuthenticationSchemes = "DriverAuth")]
    public class DriverController : Controller
    {
        private readonly IDriverPortalService _driverPortalService;

        public DriverController(IDriverPortalService driverPortalService)
        {
            _driverPortalService = driverPortalService;
        }

        public async Task<IActionResult> Index()
        {
            var assignments = await _driverPortalService.GetAssignmentsAsync(GetCurrentDriverId());
            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int assignmentId)
        {
            try
            {
                await _driverPortalService.AcceptAssignmentAsync(assignmentId, GetCurrentDriverId());
                TempData["SuccessMessage"] = "Trip accepted.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int assignmentId)
        {
            try
            {
                await _driverPortalService.RejectAssignmentAsync(assignmentId, GetCurrentDriverId());
                TempData["SuccessMessage"] = "Trip rejected.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            try
            {
                await _driverPortalService.UpdateTripStatusAsync(bookingId, GetCurrentDriverId(), newStatus);
                TempData["SuccessMessage"] = "Status updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] // low-risk telemetry write scoped to the authenticated driver's own location
        public async Task<IActionResult> ReportLocation([FromBody] LocationReportViewModel model)
        {
            await _driverPortalService.RecordLocationAsync(
                GetCurrentDriverId(), model.BookingId, model.Latitude, model.Longitude, model.AccuracyMeters, model.SpeedKmh);
            return Ok();
        }

        private int GetCurrentDriverId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
