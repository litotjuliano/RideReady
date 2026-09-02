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
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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
                PickupDate = new DateOnly(2026, 9, 5), // Future date from 2026-09-01
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car",
                PaymentMethod = "Pay_at_Pickup",
                AcceptedTerms = true
            };

            // Act
            var booking = await service.CreateBookingAsync(request);

            // Assert
            Assert.NotNull(booking);
            Assert.NotEmpty(booking.BookingReference);
            Assert.StartsWith("RR-", booking.BookingReference);
            Assert.Equal("New", booking.Status);
            Assert.Equal("Pay_at_Pickup", booking.Quote?.PaymentMethod);
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
                PickupDate = new DateOnly(2026, 9, 5), // Future date from 2026-09-01
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car",
                PaymentMethod = "Pay_at_Pickup",
                AcceptedTerms = true
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

        private class ThrowingLocationService : ILocationService
        {
            public Task<decimal> GetDistanceAsync(string pickup, string destination) =>
                throw new InvalidOperationException("Google Directions API returned status: REQUEST_DENIED");

            public Task<decimal> GetDurationAsync(string pickup, string destination) =>
                throw new InvalidOperationException("Google Directions API returned status: REQUEST_DENIED");
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
        public async Task CreateBooking_WhenLocationServiceFails_StillCreatesBookingWithZeroedQuote()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedPricingSettings(context);
            var service = new BookingService(context, new ThrowingLocationService());

            // Act
            var booking = await service.CreateBookingAsync(ValidRequest());

            // Assert
            Assert.NotNull(booking);
            Assert.NotEmpty(booking.BookingReference);
            Assert.NotNull(booking.Quote);
            Assert.Equal(0, booking.Quote!.TotalEstimatedFare);
            Assert.Equal("Pay_at_Pickup", booking.Quote.PaymentMethod);
            Assert.Null(booking.Quote.ActualFare);
        }

        [Fact]
        public async Task CreateBooking_WhenNoPricingConfiguredForVehicleType_StillCreatesBookingWithZeroedQuote()
        {
            // Arrange: no SeedPricingSettings() call, so GetQuoteAsync throws "Pricing not configured for Car"
            var context = GetInMemoryDbContext();
            var service = new BookingService(context);

            // Act
            var booking = await service.CreateBookingAsync(ValidRequest());

            // Assert
            Assert.NotNull(booking);
            Assert.NotNull(booking.Quote);
            Assert.Equal(0, booking.Quote!.TotalEstimatedFare);
        }

        [Fact]
        public async Task SetManualFareAsync_WithValidFare_UpdatesTotalEstimatedFareAndActualFare()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new BookingService(context, new ThrowingLocationService());
            var booking = await service.CreateBookingAsync(ValidRequest());

            // Act
            await service.SetManualFareAsync(booking.Id, 275.50m);

            // Assert
            var quote = await context.BookingQuotes.FirstAsync(q => q.BookingId == booking.Id);
            Assert.Equal(275.50m, quote.TotalEstimatedFare);
            Assert.Equal(275.50m, quote.ActualFare);
        }

        [Fact]
        public async Task SetManualFareAsync_WithZeroOrNegativeFare_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new BookingService(context, new ThrowingLocationService());
            var booking = await service.CreateBookingAsync(ValidRequest());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetManualFareAsync(booking.Id, 0m));
        }

        [Fact]
        public async Task SetManualFareAsync_WithNonexistentBooking_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new BookingService(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetManualFareAsync(9999, 50m));
        }

        [Fact]
        public async Task SetManualFareAsync_WhenBookingNoLongerNew_ThrowsAndDoesNotChangeFare()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new BookingService(context, new ThrowingLocationService());
            var booking = await service.CreateBookingAsync(ValidRequest());
            await service.SetManualFareAsync(booking.Id, 100m);

            var trackedBooking = await context.Bookings.FindAsync(booking.Id);
            trackedBooking!.Status = "Confirmed";
            await context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetManualFareAsync(booking.Id, 200m));
            Assert.Contains("Confirmed", ex.Message);

            var quote = await context.BookingQuotes.FirstAsync(q => q.BookingId == booking.Id);
            Assert.Equal(100m, quote.TotalEstimatedFare);
        }
    }
}
