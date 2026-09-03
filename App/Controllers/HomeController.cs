using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RideReady.Models;
using RideReady.ViewModels;

namespace RideReady.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        var model = new BookingRequestViewModel
        {
            PickupDate = DateOnly.FromDateTime(DateTime.Today)
        };
        ViewData["HideNav"] = true;
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
