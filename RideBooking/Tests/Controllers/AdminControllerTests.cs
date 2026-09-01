using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Controllers
{
    public class AdminControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        [Fact]
        public async Task Index_ReturnsViewWithBookingList()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsAssignableFrom<List<AdminBookingListItemViewModel>>(view.Model);
        }

        [Fact]
        public async Task CreateDriver_WithValidModel_RedirectsToIndex()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service)
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
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await context.Drivers.CountAsync());
        }

        private async Task<(RideBookingDbContext Context, Models.Booking Booking, Models.Driver Driver)> SeedBookingAndDriverAsync()
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
            var controller = new AdminController(new DriverAssignmentService(context))
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
            var controller = new AdminController(new DriverAssignmentService(context))
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
            var controller = new AdminController(new DriverAssignmentService(context))
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
            var controller = new AdminController(new DriverAssignmentService(context))
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

        internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
