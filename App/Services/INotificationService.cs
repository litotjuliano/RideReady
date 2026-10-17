namespace RideReady.Services
{
    public interface INotificationService
    {
        Task SendBookingCreatedNotificationAsync(int bookingId);
        Task SendDriverAssignedNotificationAsync(int bookingId, int driverId);
        Task SendDriverAcceptedNotificationAsync(int bookingId);

        /// <summary>
        /// Admin-triggered: sends the customer the accepted driver's name, phone and vehicle by email and WhatsApp.
        /// Returns whether the WhatsApp message was sent. Throws if no driver has accepted yet.
        /// </summary>
        Task<bool> SendDriverDetailsToCustomerAsync(int bookingId);

        Task SendBookingCompletedNotificationAsync(int bookingId);
        Task SendBookingCancelledNotificationAsync(int bookingId);
        Task SendPriceSetNotificationAsync(int bookingId);
        Task SendTripStatusUpdateNotificationAsync(int bookingId, string newStatus);
        Task SendUnassignedReminderAsync(int bookingId, bool urgent);
        Task SendNoShowNotificationAsync(int bookingId);
    }
}
