using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Controllers
{
    public class DriverControllerTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private static INotificationService BuildNotificationService(RideReadyDbContext context) =>
            new NotificationService(
                context,
                new RideReady.Tests.Services.FakeEmailSender(),
                new RideReady.Tests.Services.FakeWhatsAppSender(),
                new RideReady.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    SenderEmail = "noreply@rideready.my",
                    SenderName = "RideReady",
                    OperatorEmail = "operator@rideready.my"
                }));

        private static DriverController WithAuthenticatedDriver(RideReadyDbContext context, IDriverPortalService service, int driverId)
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

        private async Task<(RideReadyDbContext Context, Driver Driver, Booking Booking, DriverAssignment Assignment)> SeedAssignedBookingAsync()
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
