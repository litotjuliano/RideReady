using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IDriverAssignmentService
    {
        Task<List<AdminBookingListItemViewModel>> GetDashboardBookingsAsync();
        Task<List<Driver>> GetActiveDriversAsync();
        Task<List<Driver>> GetAllDriversAsync();
        Task<Driver> CreateDriverAsync(CreateDriverViewModel request);
        Task AssignDriverAsync(int bookingId, int driverId);
        Task UpdateBookingStatusAsync(int bookingId, string newStatus, string changedBy);
    }
}
