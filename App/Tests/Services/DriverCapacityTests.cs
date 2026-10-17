using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Services
{
    public class DriverCapacityTests
    {
        private static RideReadyDbContext NewContext() => new(new DbContextOptionsBuilder<RideReadyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        private static int _phoneCounter;

        // Every test driver gets its own phone number (drivers must be unique by phone), unless a test sets one.
        private static CreateDriverViewModel NewDriver(string name, string type, int? seats, string? phone = null) => new()
        {
            Name = name,
            Phone = phone ?? $"01234{System.Threading.Interlocked.Increment(ref _phoneCounter):D5}",
            VehicleType = type,
            VehicleNumber = "ABC 1234",
            PassengerCapacity = seats,
            Pin = "1234"
        };

        private static async Task<Booking> SeedBookingAsync(RideReadyDbContext context, int passengers)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-CAP00001",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 10, 20),
                PickupTime = new TimeOnly(9, 0),
                Passengers = passengers,
                Bags = 0,
                RequestedVehicleType = "Van",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        // ---- saving the capacity ----

        [Fact]
        public async Task CreateDriver_StoresThePassengerCapacity()
        {
            var context = NewContext();

            var driver = await new DriverAssignmentService(context).CreateDriverAsync(NewDriver("Mr Lee", "Van", 14));

            Assert.Equal(14, (await context.Drivers.FindAsync(driver.Id))!.PassengerCapacity);
        }

        [Fact]
        public async Task UpdateDriver_ChangesEveryEditableField()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            var driver = await service.CreateDriverAsync(NewDriver("Mr Lee", "Van", null));

            await service.UpdateDriverAsync(new EditDriverViewModel
            {
                Id = driver.Id, Name = "  Mr Lee Jr  ", Phone = "0187654321", VehicleType = "Bus", VehicleNumber = " JSE 8690 ",
                PassengerCapacity = 30, IsActive = false
            });

            var saved = await context.Drivers.FindAsync(driver.Id);
            Assert.Equal("Mr Lee Jr", saved!.Name);
            Assert.Equal("0187654321", saved.Phone);
            Assert.Equal("Bus", saved.VehicleType);
            Assert.Equal("JSE 8690", saved.VehicleNumber);
            Assert.Equal(30, saved.PassengerCapacity);
            Assert.False(saved.IsActive);
            Assert.NotEqual(string.Empty, saved.PinHash); // the PIN is untouched by an edit
        }

        [Fact]
        public async Task UpdateDriver_WithUnknownId_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new DriverAssignmentService(NewContext()).UpdateDriverAsync(
                new EditDriverViewModel { Id = 999, Name = "x", Phone = "0123456789", VehicleType = "Car", VehicleNumber = "A 1", PassengerCapacity = 4 }));
        }

        [Fact]
        public async Task CreateDriver_WithAPhoneAnotherDriverUses_IsRefusedWithAFriendlyMessage()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            await service.CreateDriverAsync(NewDriver("Mr Lee", "Van", 14, "0123456789"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDriverAsync(NewDriver("Mr Syed", "Bus", 30, "0123456789")));

            Assert.Equal("Another driver already uses that phone number.", ex.Message);
            Assert.Single(context.Drivers);
        }

        [Fact]
        public async Task UpdateDriver_CanKeepItsOwnPhone_ButNotTakeAnotherDriversPhone()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            var lee = await service.CreateDriverAsync(NewDriver("Mr Lee", "Van", 14, "0123456789"));
            var syed = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Mr Syed", Phone = "0198765432", VehicleType = "Bus", VehicleNumber = "BMC 5455", PassengerCapacity = 30, Pin = "1234"
            });

            EditDriverViewModel Edit(int id, string phone) => new()
            {
                Id = id, Name = "Same", Phone = phone, VehicleType = "Van", VehicleNumber = "A 1", PassengerCapacity = 10
            };

            await service.UpdateDriverAsync(Edit(lee.Id, "0123456789")); // its own number: fine
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateDriverAsync(Edit(syed.Id, "0123456789")));

            Assert.Equal("Another driver already uses that phone number.", ex.Message);
            Assert.Equal("0198765432", (await context.Drivers.FindAsync(syed.Id))!.Phone);
        }

        [Fact]
        public async Task AdminCreateDriver_WithADuplicatePhone_ShowsAMessageInsteadOfFailing()
        {
            var context = NewContext();
            await new DriverAssignmentService(context).CreateDriverAsync(NewDriver("Mr Lee", "Van", 14, "0123456789"));
            var controller = NewAdminController(context);

            var result = await controller.CreateDriver(NewDriver("Mr Syed", "Bus", 30, "0123456789"));

            Assert.Equal(nameof(AdminController.Drivers), Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("Another driver already uses that phone number.", controller.TempData["ErrorMessage"]);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(0, false)]
        [InlineData(101, false)]
        [InlineData(1, true)]
        [InlineData(14, true)]
        [InlineData(100, true)]
        public void CapacityIsRequiredAndBetween1And100(int? seats, bool valid)
        {
            foreach (object model in new object[]
            {
                NewDriver("Mr Lee", "Van", seats),
                new EditDriverViewModel { Id = 1, Name = "Mr Lee", Phone = "0123456789", VehicleType = "Van", VehicleNumber = "A 1", PassengerCapacity = seats }
            })
            {
                var results = new List<ValidationResult>();
                Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
                Assert.Equal(valid, !results.Any(r => r.MemberNames.Contains("PassengerCapacity")));
            }
        }

        // ---- assigning ----

        [Fact]
        public async Task Assign_RefusesAVehicleTooSmallForTheBooking()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context, passengers: 6);
            var driver = await service.CreateDriverAsync(NewDriver("Ah Seng", "Car", 4));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignDriverAsync(booking.Id, driver.Id));

            Assert.Contains("carries up to 4 passengers, but this booking has 6", ex.Message);
            Assert.Empty(context.DriverAssignments);
        }

        [Fact]
        public async Task Assign_AllowsAVehicleThatFits_AndOneWithNoRecordedCapacity()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context, passengers: 6);
            var bigEnough = await service.CreateDriverAsync(NewDriver("Mr Lee", "Van", 6));
            var unknown = await service.CreateDriverAsync(NewDriver("Mr Syed", "Bus", null));

            await service.AssignDriverAsync(booking.Id, bigEnough.Id);
            await service.AssignDriverAsync(booking.Id, unknown.Id); // reassign: no capacity recorded, so no check

            Assert.Equal(unknown.Id, (await context.DriverAssignments.OrderBy(a => a.Id).LastAsync()).DriverId);
        }

        // ---- admin controller ----

        private static AdminController NewAdminController(RideReadyDbContext context)
        {
            var notifier = new NotificationService(context, new FakeEmailSender(), new FakeWhatsAppSender(), new FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings { SenderEmail = "n@r.my", SenderName = "RideReady", OperatorEmail = "o@r.my" }),
                Microsoft.Extensions.Options.Options.Create(new WhatsAppSettings { ApiUrl = "https://x", AccessToken = "t", PhoneNumberId = "1", OperatorPhone = "0192462592" }));
            var controller = new AdminController(new DriverAssignmentService(context), notifier, new BookingService(context), new DriverAvailabilityService(context));
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(), new RideReady.Tests.Controllers.NullTempDataProvider());
            return controller;
        }

        [Fact]
        public async Task Dashboard_MarksVehiclesTooSmallForTheBooking()
        {
            var context = NewContext();
            var service = new DriverAssignmentService(context);
            await SeedBookingAsync(context, passengers: 6);
            var small = await service.CreateDriverAsync(NewDriver("Small", "Van", 4));
            var big = await service.CreateDriverAsync(NewDriver("Big", "Van", 14));
            var unknown = await service.CreateDriverAsync(NewDriver("Unknown", "Van", null));

            var result = await NewAdminController(context).Index();

            var item = Assert.Single(Assert.IsType<List<AdminBookingListItemViewModel>>(Assert.IsType<ViewResult>(result).Model));
            Assert.Equal("only 4 seats", item.DriverConflicts[small.Id]);
            Assert.False(item.DriverConflicts.ContainsKey(big.Id));
            Assert.False(item.DriverConflicts.ContainsKey(unknown.Id));
        }

        [Fact]
        public async Task EditDriver_Get_ShowsTheCurrentDetails()
        {
            var context = NewContext();
            var driver = await new DriverAssignmentService(context).CreateDriverAsync(NewDriver("Mr Lee", "Van", 14));

            var result = await NewAdminController(context).EditDriver(driver.Id);

            var model = Assert.IsType<EditDriverViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("Mr Lee", model.Name);
            Assert.Equal(14, model.PassengerCapacity);
            Assert.True(model.IsActive);
        }

        [Fact]
        public async Task EditDriver_Get_ForAnUnknownDriver_GoesBackToTheList()
        {
            var controller = NewAdminController(NewContext());

            var result = await controller.EditDriver(404);

            Assert.Equal(nameof(AdminController.Drivers), Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("Driver not found.", controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task EditDriver_Post_SavesAndReturnsToTheList()
        {
            var context = NewContext();
            var driver = await new DriverAssignmentService(context).CreateDriverAsync(NewDriver("Mr Lee", "Van", null));
            var controller = NewAdminController(context);

            var result = await controller.EditDriver(new EditDriverViewModel
            {
                Id = driver.Id, Name = "Mr Lee", Phone = "0185728688", VehicleType = "Van", VehicleNumber = "JSE 8690", PassengerCapacity = 12, IsActive = true
            });

            Assert.Equal(nameof(AdminController.Drivers), Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(12, (await context.Drivers.FindAsync(driver.Id))!.PassengerCapacity);
            Assert.Equal("Driver updated.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task EditDriver_Post_WithInvalidDetails_ShowsTheFormAgain()
        {
            var context = NewContext();
            var driver = await new DriverAssignmentService(context).CreateDriverAsync(NewDriver("Mr Lee", "Van", 8));
            var controller = NewAdminController(context);
            controller.ModelState.AddModelError("PassengerCapacity", "Enter how many passengers the vehicle carries");

            var result = await controller.EditDriver(new EditDriverViewModel { Id = driver.Id, Name = "Mr Lee", PassengerCapacity = null });

            Assert.IsType<ViewResult>(result);
            Assert.Equal(8, (await context.Drivers.FindAsync(driver.Id))!.PassengerCapacity);
        }
    }
}
