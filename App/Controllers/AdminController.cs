using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideReady.Services;
using RideReady.ViewModels;

namespace RideReady.Controllers
{
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AdminController : Controller
    {
        private readonly IDriverAssignmentService _driverAssignmentService;
        private readonly INotificationService _notificationService;
        private readonly IBookingService _bookingService;
        private readonly IDriverAvailabilityService _driverAvailabilityService;

        public AdminController(
            IDriverAssignmentService driverAssignmentService,
            INotificationService notificationService,
            IBookingService bookingService,
            IDriverAvailabilityService driverAvailabilityService)
        {
            _driverAssignmentService = driverAssignmentService;
            _notificationService = notificationService;
            _bookingService = bookingService;
            _driverAvailabilityService = driverAvailabilityService;
        }

        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            var activeDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            ViewBag.ActiveDrivers = activeDrivers;

            var distinctDates = bookings.Select(b => b.PickupDate).Distinct();
            var schedulesByDate = new Dictionary<DateOnly, List<DriverDayScheduleViewModel>>();
            foreach (var date in distinctDates)
            {
                schedulesByDate[date] = await _driverAvailabilityService.GetDriverDayScheduleAsync(date);
            }

            foreach (var booking in bookings)
            {
                var schedule = schedulesByDate[booking.PickupDate];
                foreach (var driver in schedule)
                {
                    string? reason = null;

                    if (driver.IsOnTimeOff)
                    {
                        reason = "on time off";
                    }
                    else
                    {
                        var block = driver.Blocks.FirstOrDefault(b =>
                            b.BookingId != booking.BookingId
                            && booking.PickupTime >= b.Start
                            && booking.PickupTime < b.End);

                        if (block != null)
                        {
                            reason = $"busy: {block.BookingReference} {block.Start:HH:mm}-{block.End:HH:mm}";
                        }
                    }

                    if (reason != null)
                    {
                        booking.DriverConflicts[driver.DriverId] = reason;
                    }
                }
            }

            return View(bookings);
        }

        public async Task<IActionResult> Drivers()
        {
            var drivers = await _driverAssignmentService.GetAllDriversAsync();
            return View(drivers);
        }

        public async Task<IActionResult> Calendar(DateOnly? date, string? view = null)
        {
            var day = date ?? DateOnly.FromDateTime(DateTime.Today);
            var calendarView = view == "month" ? "month" : "day";
            ViewBag.SelectedDate = day;
            ViewBag.CalendarView = calendarView;

            if (calendarView == "month")
            {
                ViewBag.MonthSchedule = await _driverAvailabilityService.GetDriverMonthScheduleAsync(day.Year, day.Month);
                return View(new List<DriverDayScheduleViewModel>());
            }

            var schedule = await _driverAvailabilityService.GetDriverDayScheduleAsync(day);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTimeOff(AddTimeOffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Choose a driver and a valid date range.";
                return RedirectToAction(nameof(Calendar), new { date = model.StartDate });
            }

            try
            {
                await _driverAvailabilityService.AddTimeOffAsync(model.DriverId, model.StartDate, model.EndDate, model.Reason);
                TempData["SuccessMessage"] = "Time off added.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Calendar), new { date = model.StartDate });
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
                await _notificationService.SendPriceSetNotificationAsync(model.BookingId);
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
