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
                }),
                Microsoft.Extensions.Options.Options.Create(new WhatsAppSettings
                {
                    ApiUrl = "https://graph.facebook.com/v18.0",
                    AccessToken = "test-token",
                    PhoneNumberId = "1234567890",
                    OperatorPhone = "0192462592"
                }));

        private static AdminController BuildController(RideReadyDbContext context, bool withTempData = true)
        {
            var controller = new AdminController(
                new DriverAssignmentService(context),
                BuildNotificationService(context),
                new BookingService(context),
                new DriverAvailabilityService(context));

            if (withTempData)
            {
                controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider());
            }

            return controller;
        }

        [Fact]
        public async Task Index_ReturnsViewWithBookingList()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsAssignableFrom<List<AdminBookingListItemViewModel>>(view.Model);
        }

        [Fact]
        public async Task Index_AnnotatesDriverConflictsForBusyDrivers()
        {
            // Arrange
            var (context, booking, driver) = await SeedBookingAndDriverAsync();
            var assignmentService = new DriverAssignmentService(context);
            await assignmentService.AssignDriverAsync(booking.Id, driver.Id);

            // A second booking at the same pickup time as `booking`, for the same driver
            var customer = new Models.Customer { Name = "Second Customer", Phone = "0129998888", Email = "second@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            var secondBooking = new Models.Booking
            {
                BookingReference = "RR-SECOND01",
                CustomerId = customer.Id,
                PickupLocation = "Mid Valley",
                Destination = "KLIA Terminal 1",
                PickupDate = booking.PickupDate,
                PickupTime = booking.PickupTime,
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(secondBooking);
            await context.SaveChangesAsync();

            var controller = BuildController(context, withTempData: false);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var bookings = Assert.IsType<List<AdminBookingListItemViewModel>>(view.Model);
            var secondBookingItem = bookings.Single(b => b.BookingReference == "RR-SECOND01");
            Assert.True(secondBookingItem.DriverConflicts.ContainsKey(driver.Id));
            Assert.Contains(booking.BookingReference, secondBookingItem.DriverConflicts[driver.Id]);

            var firstBookingItem = bookings.Single(b => b.BookingId == booking.Id);
            Assert.False(firstBookingItem.DriverConflicts.ContainsKey(driver.Id));
        }

        [Fact]
        public async Task CreateDriver_WithValidModel_RedirectsToDrivers()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context);
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
            var controller = BuildController(context, withTempData: false);

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
            var controller = BuildController(context);

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
            var controller = BuildController(context);

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
            var controller = BuildController(context);

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
            var controller = BuildController(context);

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

            var controller = BuildController(context);

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
            var controller = BuildController(context);

            // Act
            var result = await controller.SetFare(new SetFareViewModel { BookingId = 9999, Fare = 50m });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task Calendar_WithNoDateParam_DefaultsToTodayAndReturnsSchedule()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);
            await new DriverAssignmentService(context).CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            var result = await controller.Calendar(null);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var schedule = Assert.IsType<List<DriverDayScheduleViewModel>>(view.Model);
            Assert.Single(schedule);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today), controller.ViewBag.SelectedDate);
        }

        [Fact]
        public async Task Calendar_WithDateParam_UsesThatDate()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);
            var requestedDate = new DateOnly(2026, 12, 25);

            // Act
            await controller.Calendar(requestedDate);

            // Assert
            Assert.Equal(requestedDate, controller.ViewBag.SelectedDate);
        }

        [Fact]
        public async Task Calendar_WithMonthView_ReturnsMonthScheduleInViewBag()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);
            await new DriverAssignmentService(context).CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            var result = await controller.Calendar(new DateOnly(2026, 9, 15), "month");

            // Assert
            Assert.IsType<ViewResult>(result);
            var monthSchedule = Assert.IsType<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>>(controller.ViewBag.MonthSchedule);
            Assert.Equal(30, monthSchedule.Count);
            Assert.Equal("month", controller.ViewBag.CalendarView);
        }

        [Fact]
        public async Task Calendar_WithoutViewParam_DefaultsToDayModeUnchanged()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);

            // Act
            var result = await controller.Calendar(new DateOnly(2026, 9, 15));

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsType<List<DriverDayScheduleViewModel>>(view.Model);
            Assert.Equal("day", controller.ViewBag.CalendarView);
        }

        [Fact]
        public async Task AddTimeOff_WithValidModel_RedirectsToCalendarAndCreatesTimeOff()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context);
            var driver = await new DriverAssignmentService(context).CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            var result = await controller.AddTimeOff(new AddTimeOffViewModel
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 9, 20),
                EndDate = new DateOnly(2026, 9, 22),
                Reason = "Annual leave"
            });

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Calendar", redirect.ActionName);
            Assert.Equal(1, await context.DriverTimeOffs.CountAsync());
            Assert.Equal("Time off added.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task AddTimeOff_WithConflictingAssignment_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, booking, driver) = await SeedBookingAndDriverAsync();
            var controller = BuildController(context);
            await new DriverAssignmentService(context).AssignDriverAsync(booking.Id, driver.Id);

            // Act
            var result = await controller.AddTimeOff(new AddTimeOffViewModel
            {
                DriverId = driver.Id,
                StartDate = booking.PickupDate,
                EndDate = booking.PickupDate,
                Reason = null
            });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
            Assert.Equal(0, await context.DriverTimeOffs.CountAsync());
        }

        internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
