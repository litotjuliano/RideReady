using RideReady.Models;

namespace RideReady.Services
{
    public interface ICalendarSyncService
    {
        Task CreateOrUpdateEventAsync(Booking booking);
    }
}
