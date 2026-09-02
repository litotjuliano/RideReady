using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public class DriverAssignmentService : IDriverAssignmentService
    {
        private static readonly string[] ValidStatuses =
        {
            "New", "Confirmed", "Driver_Assigned", "Picked_Up", "In_Transit",
            "Dropped_Off", "Completed", "Cancelled", "No_Show"
        };

        private readonly RideBookingDbContext _context;

        public DriverAssignmentService(RideBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<AdminBookingListItemViewModel>> GetDashboardBookingsAsync()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Quote)
                .Where(b => b.Status != "Completed" && b.Status != "Cancelled")
                .OrderBy(b => b.PickupDate)
                .ThenBy(b => b.PickupTime)
                .ToListAsync();

            var bookingIds = bookings.Select(b => b.Id).ToList();

            var allAssignments = await _context.DriverAssignments
                .Include(a => a.Driver)
                .Where(a => bookingIds.Contains(a.BookingId))
                .ToListAsync();

            var latestByBooking = allAssignments
                .GroupBy(a => a.BookingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AssignedAt).First());

            return bookings.Select(b =>
            {
                latestByBooking.TryGetValue(b.Id, out var assignment);
                return new AdminBookingListItemViewModel
                {
                    BookingId = b.Id,
                    BookingReference = b.BookingReference,
                    CustomerName = b.Customer?.Name ?? string.Empty,
                    CustomerPhone = b.Customer?.Phone ?? string.Empty,
                    PickupLocation = b.PickupLocation,
                    Destination = b.Destination,
                    PickupDate = b.PickupDate,
                    PickupTime = b.PickupTime,
                    Passengers = b.Passengers,
                    Bags = b.Bags,
                    RequestedVehicleType = b.RequestedVehicleType,
                    Status = b.Status,
                    EstimatedFare = b.Quote?.TotalEstimatedFare,
                    AssignedDriverId = assignment?.DriverId,
                    AssignedDriverName = assignment?.Driver?.Name,
                    AssignedDriverPhone = assignment?.Driver?.Phone,
                    AssignmentStatus = assignment?.AssignmentStatus
                };
            }).ToList();
        }

        public async Task<List<Driver>> GetActiveDriversAsync()
        {
            return await _context.Drivers
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Driver> CreateDriverAsync(CreateDriverViewModel request)
        {
            var driver = new Driver
            {
                Name = request.Name,
                Phone = request.Phone,
                VehicleType = request.VehicleType,
                VehicleNumber = request.VehicleNumber,
                PinHash = PasswordHasher.Hash(request.Pin)
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();
            return driver;
        }

        // Statuses in which a driver may still be assigned or reassigned — before any
        // driver has actually picked up the passenger. Once a trip is under way
        // (Picked_Up/In_Transit/Dropped_Off) or has reached a terminal state
        // (Completed/Cancelled/No_Show), reassigning would corrupt the in-progress
        // trip's status and orphan the original driver's DriverAssignment row.
        private static readonly string[] AssignableStatuses = { "New", "Confirmed", "Driver_Assigned" };

        public async Task AssignDriverAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            if (!AssignableStatuses.Contains(booking.Status))
            {
                throw new InvalidOperationException($"Cannot assign a driver — booking is already '{booking.Status}'");
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == driverId);
            if (!driverExists)
            {
                throw new InvalidOperationException($"Driver {driverId} not found");
            }

            // DriverAssignment has an implicit one-to-one relationship with Booking
            // (Booking.CurrentAssignment is a single-reference nav), which EF Core backs
            // with a unique constraint on BookingId alone — so at most one assignment row
            // can ever exist per booking. Look it up by BookingId alone (not the
            // BookingId+DriverId composite) so reassigning to a different driver updates
            // that row instead of trying to insert a second one and violating the
            // constraint.
            var existing = await _context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == bookingId);

            if (existing == null)
            {
                _context.DriverAssignments.Add(new DriverAssignment
                {
                    BookingId = bookingId,
                    DriverId = driverId,
                    AssignedAt = DateTime.UtcNow,
                    AssignmentStatus = "Pending"
                });
            }
            else
            {
                existing.DriverId = driverId;
                existing.AssignedAt = DateTime.UtcNow;
                existing.AssignmentStatus = "Pending";
                existing.AcceptedAt = null;
                existing.RejectedAt = null;
            }

            var previousStatus = booking.Status;
            booking.Status = "Driver_Assigned";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = "Driver_Assigned",
                ChangedBy = "Admin"
            });

            await _context.SaveChangesAsync();
        }

        public async Task UpdateBookingStatusAsync(int bookingId, string newStatus, string changedBy)
        {
            if (!ValidStatuses.Contains(newStatus))
            {
                throw new InvalidOperationException($"'{newStatus}' is not a valid booking status");
            }

            if (newStatus == "Driver_Assigned")
            {
                throw new InvalidOperationException("'Driver_Assigned' can only be set by assigning a driver, not via a direct status update");
            }

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
                ChangedBy = changedBy
            });

            await _context.SaveChangesAsync();
        }
    }
}
