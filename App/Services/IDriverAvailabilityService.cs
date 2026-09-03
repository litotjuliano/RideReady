using RideReady.ViewModels;

namespace RideReady.Services
{
    public interface IDriverAvailabilityService
    {
        Task<List<DriverDayScheduleViewModel>> GetDriverDayScheduleAsync(DateOnly date);
        Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null);
        Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason);
    }
}
