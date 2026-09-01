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
            var controller = new AdminController(service);
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
    }
}
