using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using RideReady.Models;

namespace RideReady.Services
{
    public class MagicLinkService : IMagicLinkService
    {
        private const string Purpose = "RideReady.MagicLink.v1";

        private readonly IDataProtector _protector;
        private readonly AppSettings _settings;
        private readonly TimeProvider _time;

        public MagicLinkService(IDataProtectionProvider dataProtection, IOptions<AppSettings> settings, TimeProvider time)
        {
            _protector = dataProtection.CreateProtector(Purpose);
            _settings = settings.Value;
            _time = time;
        }

        public string? LinkForAdmin(Booking booking) =>
            Build(MagicLinkRole.Admin, null, booking.Id, MagicLinkExpiry.For(booking.PickupDate, _time.GetUtcNow().UtcDateTime));

        public string? LinkForDriver(int driverId, Booking booking) =>
            Build(MagicLinkRole.Driver, driverId, booking.Id, MagicLinkExpiry.For(booking.PickupDate, _time.GetUtcNow().UtcDateTime));

        private string? Build(MagicLinkRole role, int? driverId, int bookingId, DateTime expiresUtc)
        {
            var baseUrl = (_settings.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                return null;
            }

            return $"{baseUrl}/l/{Create(new MagicLink(role, driverId, bookingId, expiresUtc))}";
        }

        internal string Create(MagicLink link)
        {
            var payload = string.Join("|",
                link.Role == MagicLinkRole.Admin ? "A" : "D",
                link.DriverId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                link.BookingId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                new DateTimeOffset(link.ExpiresUtc, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            return _protector.Protect(payload);
        }

        public MagicLinkReadResult Read(string token)
        {
            string payload;
            try
            {
                payload = _protector.Unprotect(token);
            }
            catch (Exception)
            {
                // Tampered, truncated, or signed with a different key: all the same to the caller.
                return new MagicLinkReadResult(MagicLinkStatus.Invalid, null);
            }

            var parts = payload.Split('|');
            if (parts.Length != 4
                || (parts[0] != "A" && parts[0] != "D")
                || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix))
            {
                return new MagicLinkReadResult(MagicLinkStatus.Invalid, null);
            }

            var role = parts[0] == "A" ? MagicLinkRole.Admin : MagicLinkRole.Driver;
            int? driverId = int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : null;
            int? bookingId = int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var b) ? b : null;
            if (role == MagicLinkRole.Driver && driverId == null)
            {
                return new MagicLinkReadResult(MagicLinkStatus.Invalid, null);
            }

            var expiresUtc = DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime;
            var link = new MagicLink(role, driverId, bookingId, expiresUtc);
            return _time.GetUtcNow().UtcDateTime > expiresUtc
                ? new MagicLinkReadResult(MagicLinkStatus.Expired, null)
                : new MagicLinkReadResult(MagicLinkStatus.Valid, link);
        }
    }

    /// <summary>When a link stops working. Pure function with an injectable clock; times are Malaysia time (UTC+8, no daylight saving).</summary>
    public static class MagicLinkExpiry
    {
        private const int MalaysiaOffsetHours = 8;

        /// <summary>The end of the day after pickup (Malaysia time), and never sooner than 24 hours from now.</summary>
        public static DateTime For(DateOnly pickupDate, DateTime utcNow)
        {
            // Pickup day + 1, at 23:59:59 in Malaysia, expressed in UTC.
            var endOfNextDayMalaysia = pickupDate.AddDays(1).ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Unspecified);
            var endUtc = DateTime.SpecifyKind(endOfNextDayMalaysia.AddHours(-MalaysiaOffsetHours), DateTimeKind.Utc);
            var minimum = utcNow.AddHours(24);
            return endUtc < minimum ? minimum : endUtc;
        }
    }
}
