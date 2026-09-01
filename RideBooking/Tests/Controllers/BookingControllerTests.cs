using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Tests.Controllers
{
    internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }

    public class BookingControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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

        private async Task<RideBookingDbContext> GetSeededDbContextAsync()
        {
            var context = GetInMemoryDbContext();
            context.PricingSettings.Add(new PricingSetting
            {
                VehicleType = "Car",
                BaseFare = 50m,
                PerKmRate = 0.80m,
                PerHourRate = 15m,
                FirstKmDistance = 10,
                FirstKmCharge = 8m,
                PassengerSurcharge = 5m,
                ServiceTaxPercent = 6m
            });
            await context.SaveChangesAsync();
            return context;
        }

        private static BookingRequestViewModel ValidRequest() => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            CustomerEmail = "sim@email.com",
            PickupLocation = "KL Visa Center",
            Destination = "Hyt Ipoh Office",
            PickupDate = new DateOnly(2026, 9, 5),
            PickupTime = new TimeOnly(13, 8),
            Passengers = 2,
            Bags = 2,
            VehicleType = "Car",
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        [Fact]
        public async Task Create_Post_WithValidRequest_RedirectsToConfirmation()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service, BuildNotificationService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };

            // Act
            var result = await controller.Create(ValidRequest());

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
            Assert.NotNull(controller.TempData["BookingReference"]);
        }

        [Fact]
        public async Task Create_Post_WithPastPickupDate_ReturnsViewWithError()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service, BuildNotificationService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };
            var request = ValidRequest();
            request.PickupDate = new DateOnly(2020, 1, 1);
            request.PickupTime = new TimeOnly(9, 0);

            // Act
            var result = await controller.Create(request);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
        }

        [Fact]
        public async Task Create_Post_WithInvalidModelState_ReturnsViewWithSameModel()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service, BuildNotificationService(context));
            controller.ModelState.AddModelError("CustomerName", "Name is required");
            var request = ValidRequest();
            request.CustomerName = string.Empty;

            // Act
            var result = await controller.Create(request);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(request, view.Model);
        }
    }
}
