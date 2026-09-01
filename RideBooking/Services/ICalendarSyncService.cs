using RideBooking.Models;

namespace RideBooking.Services
{
    public interface ICalendarSyncService
    {
        Task CreateOrUpdateEventAsync(Booking booking);
    }
}
