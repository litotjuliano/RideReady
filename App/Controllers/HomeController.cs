using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;

namespace RideReady.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IBookingService _bookingService;

    public HomeController(ILogger<HomeController> logger, IBookingService bookingService)
    {
        _logger = logger;
        _bookingService = bookingService;
    }

    // The site's front page is the booking form, so it needs the same data as BookingController.Create.
    public async Task<IActionResult> Index()
    {
        var model = new BookingRequestViewModel
        {
            PickupDate = DateOnly.FromDateTime(DateTime.Today),
            DropOffDate = DateOnly.FromDateTime(DateTime.Today)
        };
        ViewData["HideNav"] = true;
        ViewBag.VehicleCapacities = await _bookingService.GetVehicleCapacitiesAsync();
        return View("~/Views/Booking/Create.cshtml", model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
