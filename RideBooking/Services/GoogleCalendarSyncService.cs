using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;

namespace RideBooking.Services
{
    public class GoogleCalendarSyncService : ICalendarSyncService
    {
        private readonly GoogleCalendarSettings _settings;
        private readonly RideBookingDbContext _context;

        public GoogleCalendarSyncService(IOptions<GoogleCalendarSettings> settings, RideBookingDbContext context)
        {
            _settings = settings.Value;
            _context = context;
        }

        public async Task CreateOrUpdateEventAsync(Booking booking)
        {
            var credential = GoogleCredential.FromFile(_settings.ServiceAccountKeyPath)
                .CreateScoped(CalendarService.Scope.Calendar);

            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RideBooking"
            });

            var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
            var calendarEvent = new Event
            {
                Summary = $"{booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination}",
                Description = $"Passengers: {booking.Passengers}, Bags: {booking.Bags}, Vehicle: {booking.RequestedVehicleType}",
                Start = new EventDateTime { DateTimeDateTimeOffset = pickupAt },
                End = new EventDateTime { DateTimeDateTimeOffset = pickupAt.AddHours(2) }
            };

            var existing = await _context.OperatorCalendarEvents.FirstOrDefaultAsync(e => e.BookingId == booking.Id);

            if (existing?.GoogleEventId != null)
            {
                await service.Events.Update(calendarEvent, _settings.CalendarId, existing.GoogleEventId).ExecuteAsync();
                existing.SyncedAt = DateTime.UtcNow;
            }
            else
            {
                var created = await service.Events.Insert(calendarEvent, _settings.CalendarId).ExecuteAsync();
                _context.OperatorCalendarEvents.Add(new OperatorCalendarEvent
                {
                    BookingId = booking.Id,
                    GoogleEventId = created.Id,
                    SyncedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
