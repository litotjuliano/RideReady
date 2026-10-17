# Driver Calendar & Availability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give admins a day-view calendar showing each driver's schedule (bookings + time off), and surface availability conflicts before assignment on the existing dashboard.

**Architecture:** New `IDriverAvailabilityService` computes busy windows (from `DriverAssignment`/`Booking`/`BookingQuote`) and time-off (`DriverTimeOff`, a new table) per driver per day. It is consumed by a new `Admin/Calendar` page and by the existing `Admin/Index` assign dropdown, which is annotated with conflict reasons but keeps the current hard double-booking guard as the final safety net.

**Tech Stack:** ASP.NET Core 8 MVC, Entity Framework Core 8, PostgreSQL, Bootstrap 5, xUnit (EF InMemory provider for tests)

**Spec:** `docs/superpowers/specs/2026-09-03-driver-calendar-availability-design.md`

All commands below assume the working directory is `App/` (where `RideReady.csproj` lives; `global.json` at the repo root is found automatically by walking up).

---

## Task 1: `DriverTimeOff` data model, DbContext, migration

**Files:**
- Create: `Models/DriverTimeOff.cs`
- Modify: `Models/Driver.cs`
- Modify: `Data/RideReadyDbContext.cs`
- Create: `Migrations/<timestamp>_AddDriverTimeOff.cs` (generated)

**Interfaces:**
- Produces: `DriverTimeOff` entity, `RideReadyDbContext.DriverTimeOffs` DbSet

- [ ] **Step 1: Create the `DriverTimeOff` model**

```csharp
// Models/DriverTimeOff.cs
namespace RideReady.Models
{
    public class DriverTimeOff
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Add the reverse navigation on `Driver`**

In `Models/Driver.cs`, change:

```csharp
        public ICollection<DriverAssignment> Assignments { get; set; } = new List<DriverAssignment>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
```

to:

```csharp
        public ICollection<DriverAssignment> Assignments { get; set; } = new List<DriverAssignment>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
        public ICollection<DriverTimeOff> TimeOffs { get; set; } = new List<DriverTimeOff>();
```

- [ ] **Step 3: Register the DbSet and an index**

In `Data/RideReadyDbContext.cs`, add the DbSet next to the others:

```csharp
        public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
        public DbSet<OperatorCalendarEvent> OperatorCalendarEvents => Set<OperatorCalendarEvent>();
        public DbSet<DriverTimeOff> DriverTimeOffs => Set<DriverTimeOff>();
```

and add an index in `OnModelCreating`, next to the other index declarations:

```csharp
            modelBuilder.Entity<DriverAssignment>()
                .HasIndex(da => new { da.BookingId, da.DriverId })
                .IsUnique();

            modelBuilder.Entity<DriverTimeOff>()
                .HasIndex(t => t.DriverId);
```

- [ ] **Step 4: Generate the migration**

Run: `dotnet ef migrations add AddDriverTimeOff`
Expected: creates `Migrations/<timestamp>_AddDriverTimeOff.cs`, `.Designer.cs`, and updates `RideReadyDbContextModelSnapshot.cs`

- [ ] **Step 5: Verify the migration builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 6: Commit**

```bash
git add Models/DriverTimeOff.cs Models/Driver.cs Data/RideReadyDbContext.cs Migrations/
git commit -m "feat: add DriverTimeOff model and migration"
```

---

## Task 2: ViewModels for the schedule and time-off form

**Files:**
- Create: `ViewModels/DriverScheduleBlockViewModel.cs`
- Create: `ViewModels/DriverDayScheduleViewModel.cs`
- Create: `ViewModels/AddTimeOffViewModel.cs`

**Interfaces:**
- Produces: view models consumed by `IDriverAvailabilityService` and the Calendar view/controller

- [ ] **Step 1: Create `DriverScheduleBlockViewModel`**

```csharp
// ViewModels/DriverScheduleBlockViewModel.cs
namespace RideReady.ViewModels
{
    public class DriverScheduleBlockViewModel
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public TimeOnly Start { get; set; }
        public TimeOnly End { get; set; }
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Create `DriverDayScheduleViewModel`**

```csharp
// ViewModels/DriverDayScheduleViewModel.cs
namespace RideReady.ViewModels
{
    public class DriverDayScheduleViewModel
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public bool IsOnTimeOff { get; set; }
        public string? TimeOffReason { get; set; }
        public List<DriverScheduleBlockViewModel> Blocks { get; set; } = new();
    }
}
```

- [ ] **Step 3: Create `AddTimeOffViewModel`**

```csharp
// ViewModels/AddTimeOffViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideReady.ViewModels
{
    public class AddTimeOffViewModel
    {
        [Required]
        public int DriverId { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateOnly EndDate { get; set; }

        [StringLength(255)]
        public string? Reason { get; set; }
    }
}
```

- [ ] **Step 4: Verify the project builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add ViewModels/DriverScheduleBlockViewModel.cs ViewModels/DriverDayScheduleViewModel.cs ViewModels/AddTimeOffViewModel.cs
git commit -m "feat: add view models for driver day schedule and time off"
```

---

## Task 3: `IDriverAvailabilityService` — `GetDriverDayScheduleAsync`

**Files:**
- Create: `Services/IDriverAvailabilityService.cs`
- Create: `Services/DriverAvailabilityService.cs`
- Create: `Tests/Services/DriverAvailabilityServiceTests.cs`

**Interfaces:**
- Consumes: `RideReadyDbContext` (Drivers, DriverAssignments, Bookings, BookingQuotes, DriverTimeOffs)
- Produces: `IDriverAvailabilityService.GetDriverDayScheduleAsync(DateOnly)`

- [ ] **Step 1: Declare the interface (all three methods now, so later tasks don't touch this file again)**

```csharp
// Services/IDriverAvailabilityService.cs
using RideReady.ViewModels;

namespace RideReady.Services
{
    public interface IDriverAvailabilityService
    {
        Task<List<DriverDayScheduleViewModel>> GetDriverDayScheduleAsync(DateOnly date);
        Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null);
        Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason);
    }
}
```

- [ ] **Step 2: Write failing tests for `GetDriverDayScheduleAsync`**

```csharp
// Tests/Services/DriverAvailabilityServiceTests.cs
using Microsoft.EntityFrameworkCore;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Services
{
    public class DriverAvailabilityServiceTests
    {
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Driver> SeedDriverAsync(RideReadyDbContext context, string name = "Ah Seng", string phone = "0123456789")
        {
            var driver = new Driver
            {
                Name = name,
                Phone = phone,
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                PinHash = "hash"
            };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            return driver;
        }

        private async Task<Booking> SeedBookingAsync(
            RideReadyDbContext context, string reference, DateOnly pickupDate, TimeOnly pickupTime, string status = "Confirmed")
        {
            var customer = new Customer { Name = "Customer " + reference, Phone = "01" + Math.Abs(reference.GetHashCode() % 100000000), Email = "c@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = reference,
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = pickupDate,
                PickupTime = pickupTime,
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = status
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private async Task AssignAsync(RideReadyDbContext context, Booking booking, Driver driver, string assignmentStatus = "Accepted")
        {
            context.DriverAssignments.Add(new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = assignmentStatus
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForDriverWithNoAssignments_ReturnsEmptyBlocksAndNotOnTimeOff()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var entry = Assert.Single(schedule);
            Assert.Empty(entry.Blocks);
            Assert.False(entry.IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForAssignmentWithQuoteDuration_UsesQuoteDurationForBlockEnd()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-QUOTED01", date, new TimeOnly(9, 0));
            context.BookingQuotes.Add(new BookingQuote { BookingId = booking.Id, DurationHours = 1.5m, TotalEstimatedFare = 100m, PaymentMethod = "Pay_at_Pickup" });
            await context.SaveChangesAsync();
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var block = Assert.Single(schedule[0].Blocks);
            Assert.Equal(new TimeOnly(9, 0), block.Start);
            Assert.Equal(new TimeOnly(10, 30), block.End);
            Assert.Equal("RR-QUOTED01", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForAssignmentWithoutQuoteDuration_UsesTwoHourFallback()
        {
            // Arrange — booking has no BookingQuote row at all (manual-fare path never set one)
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-NOQUOTE1", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            var block = Assert.Single(schedule[0].Blocks);
            Assert.Equal(new TimeOnly(11, 0), block.End);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ExcludesCancelledAndRejectedAssignments()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var cancelled = await SeedBookingAsync(context, "RR-CANCELLED", date, new TimeOnly(9, 0), status: "Cancelled");
            await AssignAsync(context, cancelled, driver);
            var rejected = await SeedBookingAsync(context, "RR-REJECTED1", date, new TimeOnly(11, 0));
            await AssignAsync(context, rejected, driver, assignmentStatus: "Rejected");

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            Assert.Empty(schedule[0].Blocks);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ForDriverOnTimeOff_SetsIsOnTimeOffAndReason()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            context.DriverTimeOffs.Add(new DriverTimeOff { DriverId = driver.Id, StartDate = new DateOnly(2026, 9, 9), EndDate = new DateOnly(2026, 9, 11), Reason = "Sick leave" });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(date);

            // Assert
            Assert.True(schedule[0].IsOnTimeOff);
            Assert.Equal("Sick leave", schedule[0].TimeOffReason);
        }

        [Fact]
        public async Task GetDriverDayScheduleAsync_ExcludesInactiveDrivers()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            driver.IsActive = false;
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverDayScheduleAsync(new DateOnly(2026, 9, 10));

            // Assert
            Assert.Empty(schedule);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: FAIL (`DriverAvailabilityService` class not found / does not implement interface)

- [ ] **Step 4: Implement `DriverAvailabilityService.GetDriverDayScheduleAsync`**

```csharp
// Services/DriverAvailabilityService.cs
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: PASS (6 tests)

- [ ] **Step 6: Commit**

```bash
git add Services/IDriverAvailabilityService.cs Services/DriverAvailabilityService.cs Tests/Services/DriverAvailabilityServiceTests.cs
git commit -m "feat: implement driver day schedule computation"
```

---

## Task 4: `IsDriverAvailableAsync`

**Files:**
- Modify: `Services/DriverAvailabilityService.cs`
- Modify: `Tests/Services/DriverAvailabilityServiceTests.cs`

**Interfaces:**
- Produces: working `IDriverAvailabilityService.IsDriverAvailableAsync`

- [ ] **Step 1: Write failing tests**

Add to `Tests/Services/DriverAvailabilityServiceTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task IsDriverAvailableAsync_WhenDriverHasNoBookingsThatDay_ReturnsAvailable()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

            // Assert
            Assert.True(isAvailable);
            Assert.Null(reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenTimeFallsInsideAnExistingBlock_ReturnsBusyWithBookingReference()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-BUSY0001", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act — 10:00 falls inside the default 09:00-11:00 fallback block
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(10, 0));

            // Assert
            Assert.False(isAvailable);
            Assert.Contains("RR-BUSY0001", reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenDriverOnTimeOff_ReturnsBusyWithTimeOffReason()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            context.DriverTimeOffs.Add(new DriverTimeOff { DriverId = driver.Id, StartDate = date, EndDate = date });
            await context.SaveChangesAsync();

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(9, 0));

            // Assert
            Assert.False(isAvailable);
            Assert.Equal("on time off", reason);
        }

        [Fact]
        public async Task IsDriverAvailableAsync_WhenExcludingTheConflictingBookingItself_ReturnsAvailable()
        {
            // Arrange — reassigning the same booking to the same driver shouldn't flag it as busy with itself
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 10);
            var booking = await SeedBookingAsync(context, "RR-SELF0001", date, new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var (isAvailable, reason) = await service.IsDriverAvailableAsync(driver.Id, date, new TimeOnly(9, 0), excludeBookingId: booking.Id);

            // Assert
            Assert.True(isAvailable);
            Assert.Null(reason);
        }
```

- [ ] **Step 2: Run tests to verify the new ones fail**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: FAIL on the 4 new tests (`NotImplementedException`)

- [ ] **Step 3: Implement `IsDriverAvailableAsync`**

In `Services/DriverAvailabilityService.cs`, replace:

```csharp
        public Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null)
        {
            throw new NotImplementedException();
        }
```

with:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: PASS (10 tests)

- [ ] **Step 5: Commit**

```bash
git add Services/DriverAvailabilityService.cs Tests/Services/DriverAvailabilityServiceTests.cs
git commit -m "feat: implement single driver/time availability check"
```

---

## Task 5: `AddTimeOffAsync`

**Files:**
- Modify: `Services/DriverAvailabilityService.cs`
- Modify: `Tests/Services/DriverAvailabilityServiceTests.cs`

**Interfaces:**
- Produces: working `IDriverAvailabilityService.AddTimeOffAsync`

- [ ] **Step 1: Write failing tests**

Add to `Tests/Services/DriverAvailabilityServiceTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task AddTimeOffAsync_WithValidRange_CreatesTimeOffRow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act
            await service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), "Annual leave");

            // Assert
            var timeOff = await context.DriverTimeOffs.SingleAsync();
            Assert.Equal(driver.Id, timeOff.DriverId);
            Assert.Equal(new DateOnly(2026, 9, 20), timeOff.StartDate);
            Assert.Equal(new DateOnly(2026, 9, 22), timeOff.EndDate);
            Assert.Equal("Annual leave", timeOff.Reason);
        }

        [Fact]
        public async Task AddTimeOffAsync_WithEndDateBeforeStartDate_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 20), null));
        }

        [Fact]
        public async Task AddTimeOffAsync_WithNonexistentDriver_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(9999, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null));
        }

        [Fact]
        public async Task AddTimeOffAsync_WithConflictingActiveAssignment_ThrowsExceptionNamingTheBooking()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-TIMEOFF1", new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null));
            Assert.Contains("RR-TIMEOFF1", ex.Message);
        }

        [Fact]
        public async Task AddTimeOffAsync_IgnoresCancelledBookingsWhenCheckingConflicts()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-CANCELLED2", new DateOnly(2026, 9, 21), new TimeOnly(9, 0), status: "Cancelled");
            await AssignAsync(context, booking, driver);

            // Act
            await service.AddTimeOffAsync(driver.Id, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null);

            // Assert
            Assert.Equal(1, await context.DriverTimeOffs.CountAsync());
        }
```

- [ ] **Step 2: Run tests to verify the new ones fail**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: FAIL on the 5 new tests (`NotImplementedException`)

- [ ] **Step 3: Implement `AddTimeOffAsync`**

In `Services/DriverAvailabilityService.cs`, replace:

```csharp
        public Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason)
        {
            throw new NotImplementedException();
        }
```

with:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: PASS (15 tests)

- [ ] **Step 5: Commit**

```bash
git add Services/DriverAvailabilityService.cs Tests/Services/DriverAvailabilityServiceTests.cs
git commit -m "feat: implement time-off creation with active-assignment conflict guard"
```

---

## Task 6: Wire `IDriverAvailabilityService` into DI and `AdminController`

**Files:**
- Modify: `Program.cs`
- Modify: `Controllers/AdminController.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`

**Interfaces:**
- Consumes: `IDriverAvailabilityService`
- Produces: `AdminController` with a 4th constructor dependency (no new actions yet — this task is purely the wiring, kept separate so it's a small, reviewable diff before Tasks 7-9 add behavior)

- [ ] **Step 1: Register the service in `Program.cs`**

In `Program.cs`, change:

```csharp
builder.Services.AddScoped<IDriverAssignmentService, DriverAssignmentService>();
builder.Services.AddScoped<IDriverPortalService, DriverPortalService>();
```

to:

```csharp
builder.Services.AddScoped<IDriverAssignmentService, DriverAssignmentService>();
builder.Services.AddScoped<IDriverAvailabilityService, DriverAvailabilityService>();
builder.Services.AddScoped<IDriverPortalService, DriverPortalService>();
```

- [ ] **Step 2: Add the constructor dependency to `AdminController`**

In `Controllers/AdminController.cs`, change:

```csharp
        private readonly IDriverAssignmentService _driverAssignmentService;
        private readonly INotificationService _notificationService;
        private readonly IBookingService _bookingService;

        public AdminController(
            IDriverAssignmentService driverAssignmentService,
            INotificationService notificationService,
            IBookingService bookingService)
        {
            _driverAssignmentService = driverAssignmentService;
            _notificationService = notificationService;
            _bookingService = bookingService;
        }
```

to:

```csharp
        private readonly IDriverAssignmentService _driverAssignmentService;
        private readonly INotificationService _notificationService;
        private readonly IBookingService _bookingService;
        private readonly IDriverAvailabilityService _driverAvailabilityService;

        public AdminController(
            IDriverAssignmentService driverAssignmentService,
            INotificationService notificationService,
            IBookingService bookingService,
            IDriverAvailabilityService driverAvailabilityService)
        {
            _driverAssignmentService = driverAssignmentService;
            _notificationService = notificationService;
            _bookingService = bookingService;
            _driverAvailabilityService = driverAvailabilityService;
        }
```

- [ ] **Step 3: Run the build to confirm the test file now fails to compile**

Run: `dotnet build`
Expected: Build FAILS — `Tests/Controllers/AdminControllerTests.cs` has 9 calls to `new AdminController(...)` with only 3 arguments

- [ ] **Step 4: Add a private controller factory to the test class and use it everywhere**

In `Tests/Controllers/AdminControllerTests.cs`, add this private method right after `BuildNotificationService`:

```csharp
        private static AdminController BuildController(RideReadyDbContext context, bool withTempData = true)
        {
            var controller = new AdminController(
                new DriverAssignmentService(context),
                BuildNotificationService(context),
                new BookingService(context),
                new DriverAvailabilityService(context));

            if (withTempData)
            {
                controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider());
            }

            return controller;
        }
```

Then replace every existing `new AdminController(...)` call site with `BuildController(...)`:

- `Index_ReturnsViewWithBookingList`: replace
  ```csharp
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context));
  ```
  with
  ```csharp
            var controller = BuildController(context, withTempData: false);
  ```

- `CreateDriver_WithValidModel_RedirectsToDrivers`: replace
  ```csharp
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };
  ```
  with
  ```csharp
            var controller = BuildController(context);
  ```
  (drop the `var service = ...` line entirely — this test never calls `service` again, it only exercises `controller.CreateDriver`)

- `Drivers_ReturnsAllDriversIncludingInactive`: replace
  ```csharp
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service, BuildNotificationService(context), new BookingService(context));
  ```
  with
  ```csharp
            var service = new DriverAssignmentService(context);
            var controller = BuildController(context, withTempData: false);
  ```
  (keep `var service = ...` — used to seed drivers)

- `AssignDriver_WithValidModel_RedirectsAndAssigns`, `AssignDriver_WithNonexistentBooking_RedirectsWithErrorMessageInsteadOfThrowing`, `UpdateStatus_WithValidModel_RedirectsAndUpdates`, `UpdateStatus_WithInvalidStatus_RedirectsWithErrorMessageInsteadOfThrowing`, `SetFare_WithValidFare_RedirectsAndUpdatesQuote`, `SetFare_WithNonexistentBooking_RedirectsWithErrorMessageInsteadOfThrowing`: each currently has
  ```csharp
            var controller = new AdminController(new DriverAssignmentService(context), BuildNotificationService(context), new BookingService(context))
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };
  ```
  replace each occurrence with
  ```csharp
            var controller = BuildController(context);
  ```

- [ ] **Step 5: Run all Admin controller tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (9 tests, unchanged behavior)

- [ ] **Step 6: Commit**

```bash
git add Program.cs Controllers/AdminController.cs Tests/Controllers/AdminControllerTests.cs
git commit -m "feat: wire IDriverAvailabilityService into AdminController"
```

---

## Task 7: `Admin/Calendar` page — controller action and view

**Files:**
- Modify: `Controllers/AdminController.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`
- Create: `Views/Admin/Calendar.cshtml`
- Modify: `Views/Admin/_AdminHeader.cshtml`
- Modify: `wwwroot/css/site.css`

**Interfaces:**
- Consumes: `IDriverAvailabilityService.GetDriverDayScheduleAsync`
- Produces: `GET /Admin/Calendar?date=yyyy-MM-dd`

- [ ] **Step 1: Write failing controller tests**

Add to `Tests/Controllers/AdminControllerTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task Calendar_WithNoDateParam_DefaultsToTodayAndReturnsSchedule()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);
            await new DriverAssignmentService(context).CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            var result = await controller.Calendar(null);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var schedule = Assert.IsType<List<DriverDayScheduleViewModel>>(view.Model);
            Assert.Single(schedule);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today), controller.ViewBag.SelectedDate);
        }

        [Fact]
        public async Task Calendar_WithDateParam_UsesThatDate()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);
            var requestedDate = new DateOnly(2026, 12, 25);

            // Act
            await controller.Calendar(requestedDate);

            // Assert
            Assert.Equal(requestedDate, controller.ViewBag.SelectedDate);
        }
```

Add the required `using RideReady.ViewModels;` — it's already present in the file's using list, so no change needed there.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: FAIL — `AdminController` has no `Calendar` method

- [ ] **Step 3: Add the `Calendar` action**

In `Controllers/AdminController.cs`, add after the `Drivers()` action:

```csharp
        public async Task<IActionResult> Calendar(DateOnly? date)
        {
            var day = date ?? DateOnly.FromDateTime(DateTime.Today);
            var schedule = await _driverAvailabilityService.GetDriverDayScheduleAsync(day);
            ViewBag.SelectedDate = day;
            return View(schedule);
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (11 tests)

- [ ] **Step 5: Add the "Calendar" nav link**

In `Views/Admin/_AdminHeader.cshtml`, change:

```html
        <a asp-controller="Admin" asp-action="Index" class="btn btn-sm btn-outline-secondary">Dashboard</a>
        <a asp-controller="Admin" asp-action="Drivers" class="btn btn-sm btn-outline-secondary">Drivers</a>
```

to:

```html
        <a asp-controller="Admin" asp-action="Index" class="btn btn-sm btn-outline-secondary">Dashboard</a>
        <a asp-controller="Admin" asp-action="Drivers" class="btn btn-sm btn-outline-secondary">Drivers</a>
        <a asp-controller="Admin" asp-action="Calendar" class="btn btn-sm btn-outline-secondary">Calendar</a>
```

- [ ] **Step 6: Add timeline CSS**

Append to `wwwroot/css/site.css`:

```css
.ride-timeline-scale {
    position: relative;
    height: 20px;
    margin-left: 152px;
    font-size: 0.75rem;
    color: var(--ride-text-muted);
}

.ride-timeline-scale span {
    position: absolute;
    transform: translateX(-50%);
}

.ride-timeline-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 0;
    border-top: 1px solid var(--ride-border);
}

.ride-timeline-row:first-of-type {
    border-top: none;
}

.ride-timeline-label {
    width: 140px;
    flex-shrink: 0;
}

.ride-timeline-track {
    position: relative;
    flex: 1;
    height: 36px;
    background: var(--ride-bg-2);
    border-radius: 8px;
    overflow: hidden;
}

.ride-timeline-block {
    position: absolute;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    padding: 0 6px;
    font-size: 0.7rem;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    border-radius: 6px;
    text-decoration: none;
}

.ride-timeline-block--booked {
    background: var(--ride-bg-4);
    color: var(--ride-mid-green);
    border: 1px solid var(--ride-bright-green);
}

.ride-timeline-block--booked:hover {
    background: var(--ride-bright-green);
    color: #fff;
}

.ride-timeline-block--timeoff {
    left: 0;
    width: 100%;
    justify-content: center;
    background: repeating-linear-gradient(45deg, #e5e5e5, #e5e5e5 6px, #d4d4d4 6px, #d4d4d4 12px);
    color: var(--ride-text-muted);
}

.ride-timeline-actions {
    flex-shrink: 0;
    width: 200px;
}
```

- [ ] **Step 7: Create `Views/Admin/Calendar.cshtml`**

```html
@model List<RideReady.ViewModels.DriverDayScheduleViewModel>
@{
    ViewData["Title"] = "Driver Calendar";
    ViewData["PageHeading"] = "Driver calendar";
    var selectedDate = (DateOnly)ViewBag.SelectedDate;
    var prevDate = selectedDate.AddDays(-1);
    var nextDate = selectedDate.AddDays(1);
}

@functions {
    double ToPercent(TimeOnly t)
    {
        var windowStartMinutes = 6 * 60.0;
        var totalMinutes = 18 * 60.0;
        var minutes = t.Hour * 60.0 + t.Minute - windowStartMinutes;
        if (minutes < 0) minutes = 0;
        if (minutes > totalMinutes) minutes = totalMinutes;
        return minutes / totalMinutes * 100;
    }
}

@await Html.PartialAsync("_AdminHeader")

@if (TempData["SuccessMessage"] is string successMessage)
{
    <div class="alert alert-success">@successMessage</div>
}
@if (TempData["ErrorMessage"] is string errorMessage)
{
    <div class="alert alert-danger">@errorMessage</div>
}

<div class="ride-card mb-3 d-flex justify-content-between align-items-center flex-wrap gap-2">
    <div class="d-flex align-items-center gap-2">
        <a class="btn btn-sm btn-outline-secondary" asp-action="Calendar" asp-route-date="@prevDate.ToString("yyyy-MM-dd")">&larr; Prev</a>
        <form asp-action="Calendar" method="get" class="d-flex align-items-center gap-2 mb-0">
            <input type="date" name="date" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" />
            <button type="submit" class="btn btn-sm btn-outline-secondary">Go</button>
        </form>
        <a class="btn btn-sm btn-outline-secondary" asp-action="Calendar" asp-route-date="@nextDate.ToString("yyyy-MM-dd")">Next &rarr;</a>
    </div>
    <p class="fw-bold mb-0">@selectedDate.ToString("yyyy-MM-dd")</p>
</div>

<div class="ride-card">
    <div class="ride-timeline-scale">
        @for (var hour = 6; hour <= 24; hour += 3)
        {
            var markerTime = hour == 24 ? new TimeOnly(23, 59) : new TimeOnly(hour, 0);
            <span style="left: @(ToPercent(markerTime).ToString("0.##"))%">@(hour == 24 ? "24:00" : $"{hour:00}:00")</span>
        }
    </div>

    @foreach (var driver in Model)
    {
        <div class="ride-timeline-row">
            <div class="ride-timeline-label">
                <p class="fw-semibold mb-0">@driver.DriverName</p>
                <p class="text-muted small mb-0">@driver.VehicleType</p>
            </div>
            <div class="ride-timeline-track">
                @if (driver.IsOnTimeOff)
                {
                    <div class="ride-timeline-block ride-timeline-block--timeoff" title="@(driver.TimeOffReason ?? "Time off")">
                        Time off@(string.IsNullOrEmpty(driver.TimeOffReason) ? "" : $": {driver.TimeOffReason}")
                    </div>
                }
                else
                {
                    @foreach (var block in driver.Blocks)
                    {
                        @{
                            var left = ToPercent(block.Start);
                            var right = ToPercent(block.End);
                            var width = Math.Max(right - left, 1);
                        }
                        <a asp-controller="Admin" asp-action="Index" class="ride-timeline-block ride-timeline-block--booked"
                           style="left: @(left.ToString("0.##"))%; width: @(width.ToString("0.##"))%;"
                           title="@block.BookingReference: @block.PickupLocation &rarr; @block.Destination">
                            @block.Start.ToString("HH:mm")-@block.End.ToString("HH:mm") @block.BookingReference
                        </a>
                    }
                }
            </div>
            <div class="ride-timeline-actions">
                <button type="button" class="btn btn-sm btn-outline-secondary" onclick="document.getElementById('timeoff-form-@driver.DriverId').classList.toggle('d-none');">+ Time off</button>
            </div>
        </div>
    }
</div>
```

(The time-off form itself is added in Task 8, once `AddTimeOff` exists to post to.)

- [ ] **Step 8: Commit**

```bash
git add Controllers/AdminController.cs Tests/Controllers/AdminControllerTests.cs Views/Admin/Calendar.cshtml Views/Admin/_AdminHeader.cshtml wwwroot/css/site.css
git commit -m "feat: add Admin/Calendar day-view driver schedule page"
```

---

## Task 8: Time-off submission (`AddTimeOff` action + form)

**Files:**
- Modify: `Controllers/AdminController.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`
- Modify: `Views/Admin/Calendar.cshtml`

**Interfaces:**
- Consumes: `IDriverAvailabilityService.AddTimeOffAsync`
- Produces: `POST /Admin/AddTimeOff`

- [ ] **Step 1: Write failing controller tests**

Add to `Tests/Controllers/AdminControllerTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task AddTimeOff_WithValidModel_RedirectsToCalendarAndCreatesTimeOff()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context);
            var driver = await new DriverAssignmentService(context).CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            var result = await controller.AddTimeOff(new AddTimeOffViewModel
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 9, 20),
                EndDate = new DateOnly(2026, 9, 22),
                Reason = "Annual leave"
            });

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Calendar", redirect.ActionName);
            Assert.Equal(1, await context.DriverTimeOffs.CountAsync());
            Assert.Equal("Time off added.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task AddTimeOff_WithConflictingAssignment_RedirectsWithErrorMessageInsteadOfThrowing()
        {
            // Arrange
            var (context, booking, driver) = await SeedBookingAndDriverAsync();
            var controller = BuildController(context);
            await new DriverAssignmentService(context).AssignDriverAsync(booking.Id, driver.Id);

            // Act
            var result = await controller.AddTimeOff(new AddTimeOffViewModel
            {
                DriverId = driver.Id,
                StartDate = booking.PickupDate,
                EndDate = booking.PickupDate,
                Reason = null
            });

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["ErrorMessage"]);
            Assert.Equal(0, await context.DriverTimeOffs.CountAsync());
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: FAIL — `AdminController` has no `AddTimeOff` method

- [ ] **Step 3: Add the `AddTimeOff` action**

In `Controllers/AdminController.cs`, add after the `Calendar` action:

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTimeOff(AddTimeOffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Choose a driver and a valid date range.";
                return RedirectToAction(nameof(Calendar), new { date = model.StartDate });
            }

            try
            {
                await _driverAvailabilityService.AddTimeOffAsync(model.DriverId, model.StartDate, model.EndDate, model.Reason);
                TempData["SuccessMessage"] = "Time off added.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Calendar), new { date = model.StartDate });
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (13 tests)

- [ ] **Step 5: Add the time-off form to the Calendar view**

In `Views/Admin/Calendar.cshtml`, replace:

```html
            <div class="ride-timeline-actions">
                <button type="button" class="btn btn-sm btn-outline-secondary" onclick="document.getElementById('timeoff-form-@driver.DriverId').classList.toggle('d-none');">+ Time off</button>
            </div>
```

with:

```html
            <div class="ride-timeline-actions">
                <button type="button" class="btn btn-sm btn-outline-secondary" onclick="document.getElementById('timeoff-form-@driver.DriverId').classList.toggle('d-none');">+ Time off</button>
                <form id="timeoff-form-@driver.DriverId" asp-controller="Admin" asp-action="AddTimeOff" method="post" class="d-flex gap-1 mt-2 d-none flex-wrap">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="DriverId" value="@driver.DriverId" />
                    <input type="date" name="StartDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                    <input type="date" name="EndDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                    <input type="text" name="Reason" placeholder="Reason (optional)" class="form-control form-control-sm ride-input" />
                    <button type="submit" class="btn btn-sm ride-btn-primary">Save</button>
                </form>
            </div>
```

- [ ] **Step 6: Commit**

```bash
git add Controllers/AdminController.cs Tests/Controllers/AdminControllerTests.cs Views/Admin/Calendar.cshtml
git commit -m "feat: add time-off submission form to driver calendar"
```

---

## Task 9: Availability annotations on the `Admin/Index` assign dropdown

**Files:**
- Modify: `ViewModels/AdminBookingListItemViewModel.cs`
- Modify: `Controllers/AdminController.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`
- Modify: `Views/Admin/Index.cshtml`

**Interfaces:**
- Consumes: `IDriverAvailabilityService.IsDriverAvailableAsync`
- Produces: `AdminBookingListItemViewModel.DriverConflicts`, populated by `AdminController.Index`

- [ ] **Step 1: Add `DriverConflicts` to the view model**

In `ViewModels/AdminBookingListItemViewModel.cs`, change:

```csharp
        public int? AssignedDriverId { get; set; }
        public string? AssignedDriverName { get; set; }
        public string? AssignedDriverPhone { get; set; }
        public string? AssignmentStatus { get; set; }
    }
}
```

to:

```csharp
        public int? AssignedDriverId { get; set; }
        public string? AssignedDriverName { get; set; }
        public string? AssignedDriverPhone { get; set; }
        public string? AssignmentStatus { get; set; }

        // Populated by AdminController.Index — key is a driver id, value is why that
        // driver isn't free at this booking's pickup time (e.g. "busy: RR-... 09:00-11:00"
        // or "on time off"). Absence from the dictionary means no known conflict.
        public Dictionary<int, string> DriverConflicts { get; set; } = new();
    }
}
```

- [ ] **Step 2: Write a failing controller test**

Add to `Tests/Controllers/AdminControllerTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task Index_AnnotatesDriverConflictsForBusyDrivers()
        {
            // Arrange
            var (context, booking, driver) = await SeedBookingAndDriverAsync();
            var assignmentService = new DriverAssignmentService(context);
            await assignmentService.AssignDriverAsync(booking.Id, driver.Id);

            // A second booking at the same pickup time as `booking`, for the same driver
            var customer = new Models.Customer { Name = "Second Customer", Phone = "0129998888", Email = "second@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            var secondBooking = new Models.Booking
            {
                BookingReference = "RR-SECOND01",
                CustomerId = customer.Id,
                PickupLocation = "Mid Valley",
                Destination = "KLIA Terminal 1",
                PickupDate = booking.PickupDate,
                PickupTime = booking.PickupTime,
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(secondBooking);
            await context.SaveChangesAsync();

            var controller = BuildController(context, withTempData: false);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var bookings = Assert.IsType<List<AdminBookingListItemViewModel>>(view.Model);
            var secondBookingItem = bookings.Single(b => b.BookingReference == "RR-SECOND01");
            Assert.True(secondBookingItem.DriverConflicts.ContainsKey(driver.Id));
            Assert.Contains(booking.BookingReference, secondBookingItem.DriverConflicts[driver.Id]);

            var firstBookingItem = bookings.Single(b => b.BookingId == booking.Id);
            Assert.False(firstBookingItem.DriverConflicts.ContainsKey(driver.Id));
        }
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: FAIL — `DriverConflicts` is always empty (Index doesn't populate it yet)

- [ ] **Step 4: Populate `DriverConflicts` in `Index()`**

In `Controllers/AdminController.cs`, change:

```csharp
        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            ViewBag.ActiveDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            return View(bookings);
        }
```

to:

```csharp
        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            var activeDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            ViewBag.ActiveDrivers = activeDrivers;

            foreach (var booking in bookings)
            {
                foreach (var driver in activeDrivers)
                {
                    var (isAvailable, reason) = await _driverAvailabilityService.IsDriverAvailableAsync(
                        driver.Id, booking.PickupDate, booking.PickupTime, booking.BookingId);

                    if (!isAvailable && reason != null)
                    {
                        booking.DriverConflicts[driver.Id] = reason;
                    }
                }
            }

            return View(bookings);
        }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (14 tests)

- [ ] **Step 6: Annotate the dropdown options in the view**

In `Views/Admin/Index.cshtml`, replace:

```html
                    <select name="DriverId" class="form-select ride-input">
                        @foreach (var driver in dropdownDrivers)
                        {
                            @if (driver.Id == booking.AssignedDriverId)
                            {
                                <option value="@driver.Id" selected>@driver.Name (@driver.VehicleType)</option>
                            }
                            else
                            {
                                <option value="@driver.Id">@driver.Name (@driver.VehicleType)</option>
                            }
                        }
                    </select>
```

with:

```html
                    <select name="DriverId" class="form-select ride-input">
                        @foreach (var driver in dropdownDrivers)
                        {
                            @{
                                var label = $"{driver.Name} ({driver.VehicleType})";
                                var hasConflict = booking.DriverConflicts.TryGetValue(driver.Id, out var conflictReason);
                                if (hasConflict)
                                {
                                    label += $" - {conflictReason}";
                                }
                                var optionClass = hasConflict ? "text-muted" : "";
                            }
                            @if (driver.Id == booking.AssignedDriverId)
                            {
                                <option value="@driver.Id" selected class="@optionClass">@label</option>
                            }
                            else
                            {
                                <option value="@driver.Id" class="@optionClass">@label</option>
                            }
                        }
                    </select>
```

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all tests, including the pre-existing ones untouched by this task)

- [ ] **Step 8: Commit**

```bash
git add ViewModels/AdminBookingListItemViewModel.cs Controllers/AdminController.cs Tests/Controllers/AdminControllerTests.cs Views/Admin/Index.cshtml
git commit -m "feat: show driver availability conflicts in the assign dropdown"
```

---

## Task 10: Apply the migration to the local database and verify live

**Files:** None (verification task)

- [ ] **Step 1: Apply the migration**

Run: `dotnet ef database update`
Expected: `AddDriverTimeOff` migration applied successfully

- [ ] **Step 2: Run the full test suite one more time**

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 3: Manual smoke test (if a local dev stack is running, e.g. via `run.bat`)**

- Log into `/AdminAuth/Login`, then visit `/Admin/Calendar` — confirm the page loads with one row per active driver
- Create a booking, assign a driver to it from `/Admin`, then revisit `/Admin/Calendar` for that booking's pickup date — confirm the booked block appears in the driver's row at the right time
- Use the "+ Time off" form on a driver with no assignments that day — confirm it saves and the row shows the hatched "Time off" block on reload
- Try adding time off for a driver's date that already has an active assignment — confirm it's rejected with an error naming the booking
- Go back to `/Admin`, open the assign dropdown for a booking at the same pickup time as an existing assignment — confirm the busy driver's option shows the conflict suffix

- [ ] **Step 4: Note completion**

No commit needed for this task — it's verification only. If the manual smoke test surfaces a bug, fix it as a new commit and re-run `dotnet test` before considering the plan complete.
