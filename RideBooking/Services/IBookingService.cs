using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IBookingService
    {
        Task<Booking> CreateBookingAsync(BookingRequestViewModel request);
        Task<BookingQuoteViewModel> GetQuoteAsync(BookingRequestViewModel request);
        Task<Booking?> GetBookingByReferenceAsync(string reference);
    }
}
