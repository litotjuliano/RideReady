namespace RideBooking.Services
{
    public interface INotificationService
    {
        Task SendBookingCreatedNotificationAsync(int bookingId);
        Task SendDriverAssignedNotificationAsync(int bookingId, int driverId);
        Task SendDriverAcceptedNotificationAsync(int bookingId);
        Task SendBookingCompletedNotificationAsync(int bookingId);
        Task SendBookingCancelledNotificationAsync(int bookingId);
        Task SendUnassignedReminderAsync(int bookingId, bool urgent);
        Task SendNoShowNotificationAsync(int bookingId);
    }
}
