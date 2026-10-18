using RideReady.Models;

namespace RideReady.Services
{
    public enum MagicLinkRole
    {
        Admin,
        Driver
    }

    public enum MagicLinkStatus
    {
        Valid,
        Expired,
        Invalid
    }

    /// <summary>What a sign-in link says: who it signs in, for which booking, and until when (UTC).</summary>
    public sealed record MagicLink(MagicLinkRole Role, int? DriverId, int? BookingId, DateTime ExpiresUtc);

    public sealed record MagicLinkReadResult(MagicLinkStatus Status, MagicLink? Link);

    /// <summary>
    /// Signed, expiring links that sign the admin or a driver in and open the right page. A link carries no password or
    /// PIN, only a signed note of who it is for, which booking, and when it stops working.
    /// </summary>
    public interface IMagicLinkService
    {
        /// <summary>Link for the admin that opens this booking on the dashboard, or null when no public address is configured.</summary>
        string? LinkForAdmin(Booking booking);

        /// <summary>Link for a driver that signs them in and opens their trips, or null when no public address is configured.</summary>
        string? LinkForDriver(int driverId, Booking booking);

        MagicLinkReadResult Read(string token);
    }
}
