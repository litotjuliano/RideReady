using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.ViewModels;

namespace RideReady.Services
{
    public class DriverAvailabilityService : IDriverAvailabilityService
    {
        // A driver's registered PIN, vehicle, etc. aren't relevant here — only which
        // bookings actually still occupy the driver's day. A booking that's Cancelled,
        // No_Show, or Completed no longer does, and a Rejected assignment was never
        // accepted in the first place.
        private static readonly string[] InactiveBookingStatuses = { "Cancelled", "No_Show", "Completed" };
        private const decimal FallbackBlockDurationHours = 2m;

        private readonly RideReadyDbContext _context;

        public DriverAvailabilityService(RideReadyDbContext context)
        {
            _context = context;
        }

        public async Task<List<DriverDayScheduleViewModel>> GetDriverDayScheduleAsync(DateOnly date)
        {
            var drivers = await _context.Drivers
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var assignments = await _context.DriverAssignments
                .Include(a => a.Booking)
                    .ThenInclude(b => b!.Quote)
                .Where(a => a.AssignmentStatus != "Rejected"
                    && a.Booking != null
                    && a.Booking.PickupDate == date
                    && !InactiveBookingStatuses.Contains(a.Booking.Status))
                .ToListAsync();

            var timeOffs = await _context.DriverTimeOffs
                .Where(t => t.StartDate <= date && t.EndDate >= date)
                .ToListAsync();

            return drivers.Select(driver =>
            {
                var timeOff = timeOffs.FirstOrDefault(t => t.DriverId == driver.Id);
                var blocks = assignments
                    .Where(a => a.DriverId == driver.Id)
                    .Select(a => ToBlock(a.Booking!))
                    .OrderBy(b => b.Start)
                    .ToList();

                return new DriverDayScheduleViewModel
                {
                    DriverId = driver.Id,
                    DriverName = driver.Name,
                    VehicleType = driver.VehicleType,
                    IsOnTimeOff = timeOff != null,
                    TimeOffReason = timeOff?.Reason,
                    Blocks = blocks
                };
            }).ToList();
        }

        public Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null)
        {
            throw new NotImplementedException();
        }

        public Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason)
        {
            throw new NotImplementedException();
        }

        private static DriverScheduleBlockViewModel ToBlock(Booking booking)
        {
            var durationHours = booking.Quote != null && booking.Quote.DurationHours > 0
                ? booking.Quote.DurationHours
                : FallbackBlockDurationHours;
            var end = booking.PickupTime.Add(TimeSpan.FromHours((double)durationHours));

            return new DriverScheduleBlockViewModel
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                Start = booking.PickupTime,
                End = end,
                PickupLocation = booking.PickupLocation,
                Destination = booking.Destination
            };
        }
    }
}
