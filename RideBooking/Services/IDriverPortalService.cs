using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IDriverPortalService
    {
        Task<Driver?> AuthenticateAsync(string phone, string pin);
        Task<List<DriverAssignmentListItemViewModel>> GetAssignmentsAsync(int driverId);
        Task<int> AcceptAssignmentAsync(int assignmentId, int driverId);
        Task RejectAssignmentAsync(int assignmentId, int driverId);
        Task UpdateTripStatusAsync(int bookingId, int driverId, string newStatus);
        Task RecordLocationAsync(int driverId, int? bookingId, decimal latitude, decimal longitude, int? accuracyMeters, decimal? speedKmh);
    }
}
