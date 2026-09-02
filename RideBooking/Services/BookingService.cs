using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace RideBooking.Services
{
    public class BookingService : IBookingService
    {
        private readonly RideBookingDbContext _context;
        private readonly ILocationService _locationService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(RideBookingDbContext context, ILocationService? locationService = null, ILogger<BookingService>? logger = null)
        {
            _context = context;
            _locationService = locationService ?? new MockLocationService();
            _logger = logger ?? NullLogger<BookingService>.Instance;
        }

        public async Task<Booking> CreateBookingAsync(BookingRequestViewModel request)
        {
            // Validate future date and operating hours (6AM-12AM)
            var now = DateTime.UtcNow;
            var requestedDateTime = request.PickupDate.ToDateTime(request.PickupTime);

            if (requestedDateTime < now)
                throw new InvalidOperationException("Pickup date and time must be in the future");

            if (request.PickupTime.Hour < 6 || request.PickupTime.Hour >= 24)
                throw new InvalidOperationException("Bookings only available between 6AM and 12AM (midnight)");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create or get customer
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == request.CustomerPhone);
                if (customer == null)
                {
                    customer = new Customer
                    {
                        Name = request.CustomerName,
                        Phone = request.CustomerPhone,
                        Email = request.CustomerEmail
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                // Create booking
                var booking = new Booking
                {
                    BookingReference = GenerateBookingReference(),
                    CustomerId = customer.Id,
                    PickupLocation = request.PickupLocation,
                    Destination = request.Destination,
                    PickupDate = request.PickupDate,
                    PickupTime = request.PickupTime,
                    Passengers = request.Passengers,
                    Bags = request.Bags,
                    RequestedVehicleType = request.VehicleType,
                    Notes = request.Notes,
                    Status = "New"
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Create quote. If the automatic estimate fails (Google Maps unavailable/misconfigured,
                // or no pricing tier configured for this vehicle type), don't block the booking — still
                // create it with a zeroed-out quote so the operator can key in a price manually from the
                // admin dashboard once the estimate is unavailable. The customer never sees this either
                // way, since pricing is admin-only (spec §6).
                BookingQuoteViewModel quote;
                try
                {
                    quote = await GetQuoteAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Quote calculation failed for a new booking (vehicle type {VehicleType}, pickup {Pickup} -> {Destination}); creating it with a zeroed quote for manual pricing.",
                        request.VehicleType, request.PickupLocation, request.Destination);
                    quote = new BookingQuoteViewModel
                    {
                        BaseFare = 0,
                        DistanceKm = 0,
                        DistanceCharge = 0,
                        DurationHours = 0,
                        TimeCharge = 0,
                        PassengerSurcharge = 0,
                        LuggageFee = 0,
                        Subtotal = 0,
                        ServiceTax = 0,
                        TotalEstimatedFare = 0,
                        EstimatedDuration = "Pending",
                        PaymentMethods = new List<string> { "Pay_at_Pickup", "Bank_Transfer" }
                    };
                }

                var quoteEntity = new BookingQuote
                {
                    BookingId = booking.Id,
                    BaseFare = quote.BaseFare,
                    DistanceKm = quote.DistanceKm,
                    DistanceCharge = quote.DistanceCharge,
                    DurationHours = quote.DurationHours,
                    TimeCharge = quote.TimeCharge,
                    PassengerSurcharge = quote.PassengerSurcharge,
                    LuggageFee = quote.LuggageFee,
                    Subtotal = quote.Subtotal,
                    ServiceTax = quote.ServiceTax,
                    TotalEstimatedFare = quote.TotalEstimatedFare,
                    PaymentMethod = request.PaymentMethod
                };
                _context.BookingQuotes.Add(quoteEntity);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return await _context.Bookings
                    .Include(b => b.Quote)
                    .FirstAsync(b => b.Id == booking.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<BookingQuoteViewModel> GetQuoteAsync(BookingRequestViewModel request)
        {
            var pricing = await _context.PricingSettings
                .FirstOrDefaultAsync(p => p.VehicleType == request.VehicleType && p.IsActive);

            if (pricing == null)
                throw new InvalidOperationException($"Pricing not configured for {request.VehicleType}");

            var distance = await _locationService.GetDistanceAsync(request.PickupLocation, request.Destination);
            var duration = await _locationService.GetDurationAsync(request.PickupLocation, request.Destination);

            var baseFare = pricing.BaseFare;
            var distanceCharge = CalculateDistanceCharge(distance, pricing);
            var timeCharge = duration * pricing.PerHourRate;
            var passengerSurcharge = Math.Max(0, request.Passengers - 1) * (pricing.PassengerSurcharge ?? 0);
            var luggageFeePerExtra = pricing.LuggageFeePerExtra ?? 5m; // Default to 5 if not configured
            var luggageFee = Math.Max(0, request.Bags - 2) * luggageFeePerExtra;
            var subtotal = baseFare + distanceCharge + timeCharge + passengerSurcharge + luggageFee;
            var serviceTax = subtotal * (pricing.ServiceTaxPercent / 100);

            return new BookingQuoteViewModel
            {
                BaseFare = baseFare,
                DistanceKm = distance,
                DistanceCharge = distanceCharge,
                DurationHours = duration,
                TimeCharge = timeCharge,
                PassengerSurcharge = passengerSurcharge,
                LuggageFee = luggageFee,
                Subtotal = subtotal,
                ServiceTax = serviceTax,
                TotalEstimatedFare = subtotal + serviceTax,
                EstimatedDuration = FormatDuration(duration),
                PaymentMethods = new List<string> { "Pay_at_Pickup", "Bank_Transfer" }
            };
        }

        public async Task SetManualFareAsync(int bookingId, decimal fare)
        {
            if (fare <= 0)
            {
                throw new InvalidOperationException("Fare must be greater than zero");
            }

            var booking = await _context.Bookings
                .Include(b => b.Quote)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            if (booking.Quote == null)
            {
                throw new InvalidOperationException("This booking has no quote record to update");
            }

            booking.Quote.TotalEstimatedFare = fare;
            booking.Quote.ActualFare = fare;
            await _context.SaveChangesAsync();
        }

        public async Task<Booking?> GetBookingByReferenceAsync(string reference)
        {
            return await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Quote)
                .Include(b => b.CurrentAssignment)
                .FirstOrDefaultAsync(b => b.BookingReference == reference);
        }

        private decimal CalculateDistanceCharge(decimal distanceKm, PricingSetting pricing)
        {
            if (distanceKm <= pricing.FirstKmDistance)
                return pricing.FirstKmCharge ?? 0;

            var firstKmCharge = pricing.FirstKmCharge ?? 0;
            var remainingKm = distanceKm - pricing.FirstKmDistance;
            var remainingCharge = remainingKm * pricing.PerKmRate;
            return firstKmCharge + remainingCharge;
        }

        private string GenerateBookingReference()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var reference = new string(Enumerable.Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
            return $"RR-{reference}";
        }

        private string FormatDuration(decimal hours)
        {
            var totalMinutes = (int)(hours * 60);
            var hrs = totalMinutes / 60;
            var mins = totalMinutes % 60;
            return $"{hrs}h {mins}m";
        }
    }
}
