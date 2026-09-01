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

        private static DriverController WithAuthenticatedDriver(IDriverPortalService service, int driverId)
        {
            var controller = new DriverController(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, driverId.ToString()) },
                            "TestAuth"))
                    }
                }
            };
            return controller;
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
            var controller = WithAuthenticatedDriver(service, driver.Id);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<DriverAssignmentListItemViewModel>>(view.Model);
            Assert.Single(model);
        }
    }
}
