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
        public IActionResult Create()
        {
            var model = new BookingRequestViewModel
            {
                PickupDate = DateOnly.FromDateTime(DateTime.Today)
            };
            return View(model);
        }

        [HttpPost("/Booking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingRequestViewModel request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
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
                return View(request);
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
    }
}
