using Microsoft.AspNetCore.Mvc;
using RideReady.Services;
using RideReady.ViewModels;

namespace RideReady.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;

        public BookingController(IBookingService bookingService, INotificationService notificationService)
        {
            _bookingService = bookingService;
            _notificationService = notificationService;
        }

        [HttpGet("/Booking")]
        public async Task<IActionResult> Create()
        {
            var model = BookingRequestViewModel.NewForm(DateOnly.FromDateTime(DateTime.Today));
            return await FormViewAsync(model);
        }

        [HttpPost("/Booking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingRequestViewModel request)
        {
            // The vehicle must be big enough for the passengers. The limit per vehicle type comes from the
            // drivers' recorded capacities, so it is checked here (and again in the booking service).
            var capacities = await _bookingService.GetVehicleCapacitiesAsync();
            if (VehicleCapacity.ExceededMessage(capacities, request.VehicleType, request.Passengers) is string tooManyPassengers)
            {
                ModelState.AddModelError(nameof(request.Passengers), tooManyPassengers);
            }

            if (!ModelState.IsValid)
            {
                return await FormViewAsync(request);
            }

            try
            {
                var booking = await _bookingService.CreateBookingAsync(request);
                await _notificationService.SendBookingCreatedNotificationAsync(booking.Id);
                TempData["BookingReference"] = booking.BookingReference;
                return RedirectToAction(nameof(Confirmation));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return await FormViewAsync(request);
            }
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            var reference = TempData["BookingReference"] as string;
            if (string.IsNullOrEmpty(reference))
            {
                return RedirectToAction(nameof(Create));
            }

            ViewBag.BookingReference = reference;
            return View();
        }

        /// <summary>Shows the booking form, with the passenger limit per vehicle type for its hints and live check.</summary>
        private async Task<IActionResult> FormViewAsync(BookingRequestViewModel model)
        {
            ViewBag.VehicleCapacities = await _bookingService.GetVehicleCapacitiesAsync();
            return View(model);
        }
    }
}
