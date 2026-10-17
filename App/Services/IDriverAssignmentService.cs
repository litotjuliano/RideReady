using RideReady.Models;
using RideReady.ViewModels;

namespace RideReady.Services
{
    public interface IDriverAssignmentService
    {
        Task<List<AdminBookingListItemViewModel>> GetDashboardBookingsAsync(string? sort = null);
        Task<List<Driver>> GetActiveDriversAsync();
        Task<List<Driver>> GetAllDriversAsync();
        Task<Driver> CreateDriverAsync(CreateDriverViewModel request);
        Task<Driver?> GetDriverAsync(int driverId);
        Task<Driver> UpdateDriverAsync(EditDriverViewModel request);
        Task AssignDriverAsync(int bookingId, int driverId);
        Task UpdateBookingStatusAsync(int bookingId, string newStatus, string changedBy);
    }
}
