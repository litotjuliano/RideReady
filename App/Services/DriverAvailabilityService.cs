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

            var assignments = await ActiveAssignments(_context.DriverAssignments
                    .Include(a => a.Booking)
                        .ThenInclude(b => b!.Quote))
                .Where(a => a.Booking!.PickupDate == date)
                .ToListAsync();

            var timeOffs = await _context.DriverTimeOffs
                .Where(t => t.StartDate <= date && t.EndDate >= date)
                .ToListAsync();

            return drivers.Select(driver => BuildDaySchedule(driver, assignments, timeOffs)).ToList();
        }

        public async Task<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>> GetDriverMonthScheduleAsync(int year, int month)
        {
            var monthStart = new DateOnly(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var drivers = await _context.Drivers
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var assignments = await ActiveAssignments(_context.DriverAssignments
                    .Include(a => a.Booking)
                        .ThenInclude(b => b!.Quote))
                .Where(a => a.Booking!.PickupDate >= monthStart && a.Booking.PickupDate <= monthEnd)
                .ToListAsync();

            var timeOffs = await _context.DriverTimeOffs
                .Where(t => t.StartDate <= monthEnd && t.EndDate >= monthStart)
                .ToListAsync();

            var result = new Dictionary<DateOnly, List<DriverDayScheduleViewModel>>();
            for (var date = monthStart; date <= monthEnd; date = date.AddDays(1))
            {
                var assignmentsForDay = assignments.Where(a => a.Booking!.PickupDate == date).ToList();
                var timeOffsForDay = timeOffs.Where(t => t.StartDate <= date && t.EndDate >= date).ToList();
                result[date] = drivers.Select(driver => BuildDaySchedule(driver, assignmentsForDay, timeOffsForDay)).ToList();
            }

            return result;
        }

        public async Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null)
        {
            var onTimeOff = await _context.DriverTimeOffs
                .AnyAsync(t => t.DriverId == driverId && t.StartDate <= date && t.EndDate >= date);
            if (onTimeOff)
            {
                return (false, "on time off");
            }

            var assignments = await ActiveAssignments(_context.DriverAssignments
                    .Include(a => a.Booking)
                        .ThenInclude(b => b!.Quote))
                .Where(a => a.DriverId == driverId
                    && a.Booking!.PickupDate == date
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

            var conflict = await ActiveAssignments(_context.DriverAssignments
                    .Include(a => a.Booking))
                .Where(a => a.DriverId == driverId
                    && a.Booking!.PickupDate >= startDate
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

        private static IQueryable<DriverAssignment> ActiveAssignments(IQueryable<DriverAssignment> query) =>
            query.Where(a => a.AssignmentStatus != "Rejected"
                && a.Booking != null
                && !InactiveBookingStatuses.Contains(a.Booking.Status));

        // Builds one driver's schedule entry for a single day, given assignments/time-offs
        // already filtered down to that day (or, for the month view, pre-sliced per day from
        // a month's worth of data — see GetDriverMonthScheduleAsync). Shared by both callers
        // so the "which assignment counts, what a time-off looks like" rules can't drift
        // between the single-day and whole-month code paths.
        private static DriverDayScheduleViewModel BuildDaySchedule(
            Driver driver, List<DriverAssignment> assignmentsForDay, List<DriverTimeOff> timeOffsCoveringDay)
        {
            var timeOff = timeOffsCoveringDay.FirstOrDefault(t => t.DriverId == driver.Id);
            var blocks = assignmentsForDay
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
