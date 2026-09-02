using RideReady.Models;
using RideReady.ViewModels;

namespace RideReady.Services
{
    public interface IBookingService
    {
        Task<Booking> CreateBookingAsync(BookingRequestViewModel request);
        Task<BookingQuoteViewModel> GetQuoteAsync(BookingRequestViewModel request);
        Task<Booking?> GetBookingByReferenceAsync(string reference);
        Task SetManualFareAsync(int bookingId, decimal fare);
    }
}
