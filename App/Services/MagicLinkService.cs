using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using RideReady.Models;

namespace RideReady.Services
{
    /// <summary>
    /// Signed, expiring sign-in links, kept short on purpose (about 30 characters of code) because they travel in WhatsApp
    /// messages. A code is a few bytes saying who it is for, which booking and until when, followed by a truncated HMAC-SHA256
    /// signature made with <see cref="LinkSigningKey"/>. There is no password or PIN in it.
    /// </summary>
    public class MagicLinkService : IMagicLinkService
    {
        private const byte AdminTag = 0x11;
        private const byte DriverTag = 0x12;
        private const int SignatureBytes = 10; // 80 bits: far beyond guessing, and keeps the link short
        private const int AdminBodyBytes = 1 + 4 + 4;         // tag, booking, expiry
        private const int DriverBodyBytes = 1 + 4 + 4 + 4;    // tag, driver, booking, expiry

        private readonly LinkSigningKey _key;
        private readonly AppSettings _settings;
        private readonly TimeProvider _time;

        public MagicLinkService(LinkSigningKey key, IOptions<AppSettings> settings, TimeProvider time)
        {
            _key = key;
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
            var isDriver = link.Role == MagicLinkRole.Driver;
            var body = new byte[isDriver ? DriverBodyBytes : AdminBodyBytes];
            var position = 0;
            body[position++] = isDriver ? DriverTag : AdminTag;
            if (isDriver)
            {
                WriteInt(body, ref position, link.DriverId ?? throw new ArgumentException("A driver link needs a driver."));
            }
            WriteInt(body, ref position, link.BookingId ?? 0);
            WriteUInt(body, ref position, checked((uint)new DateTimeOffset(link.ExpiresUtc, TimeSpan.Zero).ToUnixTimeSeconds()));

            var signed = new byte[body.Length + SignatureBytes];
            body.CopyTo(signed, 0);
            Sign(body).AsSpan(0, SignatureBytes).CopyTo(signed.AsSpan(body.Length));
            return Base64UrlEncode(signed);
        }

        public MagicLinkReadResult Read(string token)
        {
            var invalid = new MagicLinkReadResult(MagicLinkStatus.Invalid, null);
            var bytes = Base64UrlDecode(token);
            if (bytes is null || bytes.Length < AdminBodyBytes + SignatureBytes)
            {
                return invalid;
            }

            var isDriver = bytes[0] == DriverTag;
            if (bytes[0] != AdminTag && !isDriver)
            {
                return invalid;
            }

            var bodyLength = isDriver ? DriverBodyBytes : AdminBodyBytes;
            if (bytes.Length != bodyLength + SignatureBytes)
            {
                return invalid;
            }

            var body = bytes.AsSpan(0, bodyLength).ToArray();
            var expected = Sign(body).AsSpan(0, SignatureBytes);
            if (!CryptographicOperations.FixedTimeEquals(expected, bytes.AsSpan(bodyLength, SignatureBytes)))
            {
                return invalid;
            }

            var position = 1;
            int? driverId = isDriver ? ReadInt(body, ref position) : null;
            var bookingId = ReadInt(body, ref position);
            var expiresUtc = DateTimeOffset.FromUnixTimeSeconds(ReadUInt(body, ref position)).UtcDateTime;

            var link = new MagicLink(isDriver ? MagicLinkRole.Driver : MagicLinkRole.Admin, driverId, bookingId, expiresUtc);
            return _time.GetUtcNow().UtcDateTime > expiresUtc
                ? new MagicLinkReadResult(MagicLinkStatus.Expired, null)
                : new MagicLinkReadResult(MagicLinkStatus.Valid, link);
        }

        private byte[] Sign(byte[] body)
        {
            using var hmac = new HMACSHA256(_key.Value);
            return hmac.ComputeHash(body);
        }

        private static void WriteInt(byte[] buffer, ref int position, int value) => WriteUInt(buffer, ref position, checked((uint)value));

        private static void WriteUInt(byte[] buffer, ref int position, uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(position, 4), value);
            position += 4;
        }

        private static int ReadInt(byte[] buffer, ref int position) => checked((int)ReadUInt(buffer, ref position));

        private static uint ReadUInt(byte[] buffer, ref int position)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(position, 4));
            position += 4;
            return value;
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[]? Base64UrlDecode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 200)
            {
                return null;
            }

            var padded = text.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            try
            {
                return Convert.FromBase64String(padded);
            }
            catch (FormatException)
            {
                return null;
            }
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
