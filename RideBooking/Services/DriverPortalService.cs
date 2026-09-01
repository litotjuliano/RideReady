using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public class DriverPortalService : IDriverPortalService
    {
        private static readonly string[] DriverTripStatuses = { "Picked_Up", "In_Transit", "Dropped_Off", "Completed" };

        private readonly RideBookingDbContext _context;

        public DriverPortalService(RideBookingDbContext context)
        {
            _context = context;
        }

        public async Task<Driver?> AuthenticateAsync(string phone, string pin)
        {
            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Phone == phone && d.IsActive);
            if (driver == null || !PasswordHasher.Verify(pin, driver.PinHash))
            {
                return null;
            }
            return driver;
        }

        public async Task<List<DriverAssignmentListItemViewModel>> GetAssignmentsAsync(int driverId)
        {
            var assignments = await _context.DriverAssignments
                .Include(a => a.Booking)
                    .ThenInclude(b => b!.Customer)
                .Where(a => a.DriverId == driverId && a.AssignmentStatus != "Rejected")
                .ToListAsync();

            return assignments
                .Where(a => a.Booking != null)
                .OrderBy(a => a.Booking!.PickupDate)
                .ThenBy(a => a.Booking!.PickupTime)
                .Select(a => new DriverAssignmentListItemViewModel
                {
                    AssignmentId = a.Id,
                    BookingId = a.Booking!.Id,
                    BookingReference = a.Booking.BookingReference,
                    CustomerName = a.Booking.Customer?.Name ?? string.Empty,
                    CustomerPhone = a.Booking.Customer?.Phone ?? string.Empty,
                    PickupLocation = a.Booking.PickupLocation,
                    Destination = a.Booking.Destination,
                    PickupDate = a.Booking.PickupDate,
                    PickupTime = a.Booking.PickupTime,
                    Passengers = a.Booking.Passengers,
                    Bags = a.Booking.Bags,
                    Notes = a.Booking.Notes,
                    AssignmentStatus = a.AssignmentStatus,
                    BookingStatus = a.Booking.Status
                })
                .ToList();
        }

        public async Task AcceptAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await GetOwnedAssignmentAsync(assignmentId, driverId);
            var booking = assignment.Booking ?? await _context.Bookings.FindAsync(assignment.BookingId)
                ?? throw new InvalidOperationException($"Booking {assignment.BookingId} not found");

            assignment.AssignmentStatus = "Accepted";
            assignment.AcceptedAt = DateTime.UtcNow;

            var previousStatus = booking.Status;
            booking.Status = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = previousStatus,
                NewStatus = "Confirmed",
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task RejectAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await GetOwnedAssignmentAsync(assignmentId, driverId);
            var booking = assignment.Booking ?? await _context.Bookings.FindAsync(assignment.BookingId)
                ?? throw new InvalidOperationException($"Booking {assignment.BookingId} not found");

            assignment.AssignmentStatus = "Rejected";
            assignment.RejectedAt = DateTime.UtcNow;

            var previousStatus = booking.Status;
            booking.Status = "New";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = previousStatus,
                NewStatus = "New",
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task UpdateTripStatusAsync(int bookingId, int driverId, string newStatus)
        {
            if (!DriverTripStatuses.Contains(newStatus))
            {
                throw new InvalidOperationException($"'{newStatus}' is not a status a driver can set");
            }

            var assignment = await _context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == bookingId && a.DriverId == driverId && a.AssignmentStatus == "Accepted")
                ?? throw new InvalidOperationException("No accepted assignment found for this driver and booking");

            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var previousStatus = booking.Status;
            booking.Status = newStatus;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task RecordLocationAsync(int driverId, int? bookingId, decimal latitude, decimal longitude, int? accuracyMeters, decimal? speedKmh)
        {
            _context.DriverLocations.Add(new DriverLocation
            {
                DriverId = driverId,
                BookingId = bookingId,
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracyMeters,
                SpeedKmh = speedKmh
            });

            await _context.SaveChangesAsync();
        }

        private async Task<DriverAssignment> GetOwnedAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await _context.DriverAssignments
                .Include(a => a.Booking)
                .FirstOrDefaultAsync(a => a.Id == assignmentId)
                ?? throw new InvalidOperationException($"Assignment {assignmentId} not found");

            if (assignment.DriverId != driverId)
            {
                throw new InvalidOperationException("This assignment does not belong to the current driver");
            }

            return assignment;
        }
    }
}
