using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Controllers
{
    public class DriverControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private static INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(
                context,
                new RideBooking.Tests.Services.FakeEmailSender(),
                new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    SenderEmail = "noreply@ridebooking.my",
                    SenderName = "RideBooking",
                    OperatorEmail = "operator@ridebooking.my"
                }));

        private static DriverController WithAuthenticatedDriver(RideBookingDbContext context, IDriverPortalService service, int driverId)
        {
            var controller = new DriverController(service, BuildNotificationService(context))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, driverId.ToString()) },
                            "TestAuth"))
                    }
                },
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new DefaultHttpContext(),
                    new NullTempDataProvider())
            };
            return controller;
        }

        private async Task<(RideBookingDbContext Context, Driver Driver, Booking Booking, DriverAssignment Assignment)> SeedAssignedBookingAsync()
        {
            var context = GetInMemoryDbContext();
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = PasswordHasher.Hash("1234") };
            context.Drivers.Add(driver);

            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0004",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "Driver_Assigned"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var assignment = new DriverAssignment { BookingId = booking.Id, DriverId = driver.Id, AssignmentStatus = "Pending" };
            context.DriverAssignments.Add(assignment);
            await context.SaveChangesAsync();

            return (context, driver, booking, assignment);
        }

        [Fact]
        public async Task Index_ReturnsViewWithOnlyCurrentDriversAssignments()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = PasswordHasher.Hash("1234") };
            var otherDriver = new Driver { Name = "Bob", Phone = "0198765432", VehicleType = "Car", PinHash = PasswordHasher.Hash("5678") };
            context.Drivers.AddRange(driver, otherDriver);

            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0003",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "Driver_Assigned"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            context.DriverAssignments.Add(new DriverAssignment { BookingId = booking.Id, DriverId = driver.Id, AssignmentStatus = "Pending" });
            await context.SaveChangesAsync();

            var service = new DriverPortalService(context);
            var controller = WithAuthenticatedDriver(context, service, driver.Id);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<DriverAssignmentListItemViewModel>>(view.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task Accept_ForAssignmentBelongingToAnotherDriver_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, _, _, assignment) = await SeedAssignedBookingAsync();
            var service = new DriverPortalService(context);
            var controller = WithAuthenticatedDriver(context, service, driverId: 9999);

            // Act
            var result = await controller.Accept(assignment.Id);

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Reject_ForAssignmentBelongingToAnotherDriver_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, _, _, assignment) = await SeedAssignedBookingAsync();
            var service = new DriverPortalService(context);
            var controller = WithAuthenticatedDriver(context, service, driverId: 9999);

            // Act
            var result = await controller.Reject(assignment.Id);

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task UpdateStatus_WithInvalidStatus_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, driver, booking, _) = await SeedAssignedBookingAsync();
            var service = new DriverPortalService(context);
            var controller = WithAuthenticatedDriver(context, service, driver.Id);

            // Act
            var result = await controller.UpdateStatus(booking.Id, "NotARealStatus");

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
