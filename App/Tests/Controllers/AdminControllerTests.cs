using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Controllers
{
    public class AdminControllerTests
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

        [Fact]
        public async Task Index_ReturnsViewWithBookingList()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context));

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsAssignableFrom<List<AdminBookingListItemViewModel>>(view.Model);
        }

        [Fact]
        public async Task CreateDriver_WithValidModel_RedirectsToDrivers()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };
            var model = new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            };

            // Act
            var result = await controller.CreateDriver(model);

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Drivers", redirect.ActionName);
            Assert.Equal(1, await context.Drivers.CountAsync());
        }

        [Fact]
        public async Task Drivers_ReturnsAllDriversIncludingInactive()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context));

            await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            var inactiveDriver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Kumar",
                Phone = "0129876543",
                VehicleType = "Van",
                VehicleNumber = "XYZ 9999",
                Pin = "5678"
            });
            inactiveDriver.IsActive = false;
            await context.SaveChangesAsync();

            // Act
            var result = await controller.Drivers();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var drivers = Assert.IsType<List<Models.Driver>>(view.Model);
            Assert.Equal(2, drivers.Count);
            Assert.Contains(drivers, d => d.Name == "Kumar" && !d.IsActive);
        }

        private async Task<(RideReadyDbContext Context, Models.Booking Booking, Models.Driver Driver)> SeedBookingAndDriverAsync()
        {
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);

            var customer = new Models.Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Models.Booking
            {
                BookingReference = "RR-TEST0009",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            return (context, booking, driver);
        }

        [Fact]
        public async Task AssignDriver_WithValidModel_RedirectsAndAssigns()
        {
            // Arrange
            var (context, booking, driver) = await SeedBookingAndDriverAsync();
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.AssignDriver(new AssignDriverViewModel { BookingId = booking.Id, DriverId = driver.Id });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
            Assert.Equal("Driver assigned.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task AssignDriver_WithNonexistentBooking_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, _, driver) = await SeedBookingAndDriverAsync();
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.AssignDriver(new AssignDriverViewModel { BookingId = 9999, DriverId = driver.Id });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task UpdateStatus_WithValidModel_RedirectsAndUpdates()
        {
            // Arrange
            var (context, booking, _) = await SeedBookingAndDriverAsync();
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.UpdateStatus(new UpdateStatusViewModel { BookingId = booking.Id, NewStatus = "Confirmed" });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Confirmed", updated!.Status);
            Assert.Equal("Status updated.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task UpdateStatus_WithInvalidStatus_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, booking, _) = await SeedBookingAndDriverAsync();
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.UpdateStatus(new UpdateStatusViewModel { BookingId = booking.Id, NewStatus = "NotARealStatus" });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task SetFare_WithValidFare_RedirectsAndUpdatesQuote()
        {
            // Arrange
            var (context, booking, _) = await SeedBookingAndDriverAsync();
            context.BookingQuotes.Add(new Models.BookingQuote
            {
                BookingId = booking.Id,
                TotalEstimatedFare = 0,
                PaymentMethod = "Pay_at_Pickup"
            });
            await context.SaveChangesAsync();

            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.SetFare(new SetFareViewModel { BookingId = booking.Id, Fare = 123.45m });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            var quote = await context.BookingQuotes.FirstAsync(q => q.BookingId == booking.Id);
            Assert.Equal(123.45m, quote.TotalEstimatedFare);
            Assert.Equal(123.45m, quote.ActualFare);
            Assert.Equal("Fare saved.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task SetFare_WithNonexistentBooking_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, _, _) = await SeedBookingAndDriverAsync();
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.SetFare(new SetFareViewModel { BookingId = 9999, Fare = 50m });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
