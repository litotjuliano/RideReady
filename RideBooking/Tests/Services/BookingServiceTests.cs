using Xunit;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.Data;
using Microsoft.EntityFrameworkCore;
using RideBooking.ViewModels;

namespace RideBooking.Tests.Services
{
    public class BookingServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        [Fact]
        public async Task CreateBooking_WithValidRequest_ReturnsBookingWithReference()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedPricingSettings(context);
            var service = new BookingService(context);
            var request = new BookingRequestViewModel
            {
                CustomerName = "Uncle Sim",
                CustomerPhone = "0125183838",
                CustomerEmail = "sim@email.com",
                PickupLocation = "KL Visa Center",
                Destination = "Hyt Ipoh Office",
                PickupDate = new DateOnly(2026, 8, 27),
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car"
            };

            // Act
            var booking = await service.CreateBookingAsync(request);

            // Assert
            Assert.NotNull(booking);
            Assert.NotEmpty(booking.BookingReference);
            Assert.StartsWith("RR-", booking.BookingReference);
            Assert.Equal("New", booking.Status);
        }

        [Fact]
        public async Task GetQuote_WithValidRequest_CalculatesPricingCorrectly()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedPricingSettings(context);
            var service = new BookingService(context);
            var request = new BookingRequestViewModel
            {
                CustomerName = "Uncle Sim",
                CustomerPhone = "0125183838",
                CustomerEmail = "sim@email.com",
                PickupLocation = "KL Visa Center",
                Destination = "Hyt Ipoh Office",
                PickupDate = new DateOnly(2026, 8, 27),
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car"
            };

            // Act
            var quote = await service.GetQuoteAsync(request);

            // Assert
            Assert.NotNull(quote);
            Assert.True(quote.TotalEstimatedFare > 0);
        }

        private async Task SeedPricingSettings(RideBookingDbContext context)
        {
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
        }
    }
}
