namespace RideBooking.Services
{
    public interface IWhatsAppSender
    {
        Task SendAsync(string toPhone, string message);
    }
}
