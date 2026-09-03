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

        public async Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null)
        {
            var onTimeOff = await _context.DriverTimeOffs
                .AnyAsync(t => t.DriverId == driverId && t.StartDate <= date && t.EndDate >= date);
            if (onTimeOff)
            {
                return (false, "on time off");
            }

            var assignments = await _context.DriverAssignments
                .Include(a => a.Booking)
                    .ThenInclude(b => b!.Quote)
                .Where(a => a.DriverId == driverId
                    && a.AssignmentStatus != "Rejected"
                    && a.Booking != null
                    && a.Booking.PickupDate == date
                    && !InactiveBookingStatuses.Contains(a.Booking.Status)
                    && (excludeBookingId == null || a.BookingId != excludeBookingId))
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                var block = ToBlock(assignment.Booking!);
                if (time >= block.Start && time < block.End)
                {
                    return (false, $"busy: {block.BookingReference} {block.Start:HH:mm}-{block.End:HH:mm}");
                }
            }

            return (true, null);
        }

        public async Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason)
        {
            if (endDate < startDate)
            {
                throw new InvalidOperationException("End date must be on or after the start date");
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == driverId);
            if (!driverExists)
            {
                throw new InvalidOperationException($"Driver {driverId} not found");
            }

            var conflict = await _context.DriverAssignments
                .Include(a => a.Booking)
                .Where(a => a.DriverId == driverId
                    && a.AssignmentStatus != "Rejected"
                    && a.Booking != null
                    && !InactiveBookingStatuses.Contains(a.Booking.Status)
                    && a.Booking.PickupDate >= startDate
                    && a.Booking.PickupDate <= endDate)
                .Select(a => a.Booking!.BookingReference)
                .FirstOrDefaultAsync();

            if (conflict != null)
            {
                throw new InvalidOperationException(
                    $"Cannot mark time off — driver has an active assignment ({conflict}) during that period");
            }

            _context.DriverTimeOffs.Add(new DriverTimeOff
            {
                DriverId = driverId,
                StartDate = startDate,
                EndDate = endDate,
                Reason = reason
            });

            await _context.SaveChangesAsync();
        }

        private static DriverScheduleBlockViewModel ToBlock(Booking booking)
        {
            var durationHours = booking.Quote != null && booking.Quote.DurationHours > 0
                ? booking.Quote.DurationHours
                : FallbackBlockDurationHours;
            var duration = TimeSpan.FromHours((double)durationHours);
            var end = booking.PickupTime.ToTimeSpan() + duration >= TimeSpan.FromDays(1)
                ? new TimeOnly(23, 59)
                : booking.PickupTime.Add(duration);

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
