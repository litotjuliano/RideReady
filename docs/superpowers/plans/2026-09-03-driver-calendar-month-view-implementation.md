# Driver Calendar Month View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Month view to the existing `Admin/Calendar` page (day-initial chips per driver, click-through to Day view), toggled alongside the existing Day view.

**Architecture:** A new `IDriverAvailabilityService.GetDriverMonthScheduleAsync(year, month)` method fetches a whole month's assignments/time-offs in a fixed small number of queries (not one per day) and builds each day's data via a helper factored out of the existing `GetDriverDayScheduleAsync`. `AdminController.Calendar` gains an optional `view` query parameter; day-mode behavior is completely unchanged (same model, same tests), month-mode data flows through `ViewBag` to a new `_CalendarMonth.cshtml` partial.

**Tech Stack:** ASP.NET Core 8 MVC, Entity Framework Core 8, PostgreSQL, Bootstrap 5, xUnit (EF InMemory provider for tests)

**Spec:** `docs/superpowers/specs/2026-09-03-driver-calendar-month-view-design.md`

All commands below assume the working directory is `App/` (where `RideReady.csproj` lives).

---

## Task 1: `GetDriverMonthScheduleAsync` in `DriverAvailabilityService`

**Files:**
- Modify: `Services/IDriverAvailabilityService.cs`
- Modify: `Services/DriverAvailabilityService.cs`
- Modify: `Tests/Services/DriverAvailabilityServiceTests.cs`

**Interfaces:**
- Produces: `IDriverAvailabilityService.GetDriverMonthScheduleAsync(int year, int month) -> Task<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>>`

- [ ] **Step 1: Refactor `GetDriverDayScheduleAsync` to extract a shared per-day builder (no behavior change)**

In `Services/DriverAvailabilityService.cs`, replace:

```csharp
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
```

with:

```csharp
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
```

Then add the new private helper right after `ActiveAssignments`:

```csharp
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
```

(Leave `ToBlock` exactly where it is, unchanged.)

- [ ] **Step 2: Run the existing `DriverAvailabilityServiceTests` to confirm the refactor didn't change behavior**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: PASS (all existing tests, unchanged count)

- [ ] **Step 3: Add `GetDriverMonthScheduleAsync` to the interface**

In `Services/IDriverAvailabilityService.cs`, change:

```csharp
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

to:

```csharp
namespace RideReady.Services
{
    public interface IDriverAvailabilityService
    {
        Task<List<DriverDayScheduleViewModel>> GetDriverDayScheduleAsync(DateOnly date);
        Task<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>> GetDriverMonthScheduleAsync(int year, int month);
        Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time, int? excludeBookingId = null);
        Task AddTimeOffAsync(int driverId, DateOnly startDate, DateOnly endDate, string? reason);
    }
}
```

- [ ] **Step 4: Write failing tests for `GetDriverMonthScheduleAsync`**

Add to `Tests/Services/DriverAvailabilityServiceTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task GetDriverMonthScheduleAsync_ReturnsOneEntryPerDayOfTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            await SeedDriverAsync(context);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.Equal(30, schedule.Count);
            Assert.True(schedule.ContainsKey(new DateOnly(2026, 9, 1)));
            Assert.True(schedule.ContainsKey(new DateOnly(2026, 9, 30)));
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_IncludesBookingOnFirstDayOfMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-FIRSTDAY", new DateOnly(2026, 9, 1), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var day1 = schedule[new DateOnly(2026, 9, 1)];
            var block = Assert.Single(day1[0].Blocks);
            Assert.Equal("RR-FIRSTDAY", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_IncludesBookingOnLastDayOfMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var booking = await SeedBookingAsync(context, "RR-LASTDAY0", new DateOnly(2026, 9, 30), new TimeOnly(9, 0));
            await AssignAsync(context, booking, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var lastDay = schedule[new DateOnly(2026, 9, 30)];
            var block = Assert.Single(lastDay[0].Blocks);
            Assert.Equal("RR-LASTDAY0", block.BookingReference);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_DriverWithTwoBookingsSameDay_BothBlocksInOneDayEntry()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            var date = new DateOnly(2026, 9, 15);
            var morning = await SeedBookingAsync(context, "RR-MORNING1", date, new TimeOnly(6, 0));
            await AssignAsync(context, morning, driver);
            var evening = await SeedBookingAsync(context, "RR-EVENING1", date, new TimeOnly(20, 0));
            await AssignAsync(context, evening, driver);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            var day = schedule[date];
            Assert.Equal(2, day[0].Blocks.Count);
            Assert.Contains(day[0].Blocks, b => b.BookingReference == "RR-MORNING1");
            Assert.Contains(day[0].Blocks, b => b.BookingReference == "RR-EVENING1");
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_TimeOffStartingBeforeMonthAndEndingInsideIt_AppliesWithinTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            context.DriverTimeOffs.Add(new DriverTimeOff
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 8, 28),
                EndDate = new DateOnly(2026, 9, 3),
                Reason = "Carried over leave"
            });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.True(schedule[new DateOnly(2026, 9, 1)][0].IsOnTimeOff);
            Assert.True(schedule[new DateOnly(2026, 9, 3)][0].IsOnTimeOff);
            Assert.False(schedule[new DateOnly(2026, 9, 4)][0].IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_TimeOffStartingInsideMonthAndEndingAfterIt_AppliesWithinTheMonth()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);
            var driver = await SeedDriverAsync(context);
            context.DriverTimeOffs.Add(new DriverTimeOff
            {
                DriverId = driver.Id,
                StartDate = new DateOnly(2026, 9, 28),
                EndDate = new DateOnly(2026, 10, 5),
                Reason = "Spills into next month"
            });
            await context.SaveChangesAsync();

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.True(schedule[new DateOnly(2026, 9, 28)][0].IsOnTimeOff);
            Assert.True(schedule[new DateOnly(2026, 9, 30)][0].IsOnTimeOff);
        }

        [Fact]
        public async Task GetDriverMonthScheduleAsync_WithNoActiveDrivers_ReturnsEmptyListForEveryDay()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAvailabilityService(context);

            // Act
            var schedule = await service.GetDriverMonthScheduleAsync(2026, 9);

            // Assert
            Assert.Equal(30, schedule.Count);
            Assert.All(schedule.Values, day => Assert.Empty(day));
        }
```

- [ ] **Step 5: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: FAIL — `GetDriverMonthScheduleAsync` does not exist on `DriverAvailabilityService` (and the interface won't compile until Step 6 too)

- [ ] **Step 6: Implement `GetDriverMonthScheduleAsync`**

In `Services/DriverAvailabilityService.cs`, add this new public method right after `GetDriverDayScheduleAsync`:

```csharp
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
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DriverAvailabilityServiceTests`
Expected: PASS (all previously-passing tests + 7 new ones)

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all tests, no regressions from the `GetDriverDayScheduleAsync` refactor)

- [ ] **Step 9: Commit**

```bash
git add Services/IDriverAvailabilityService.cs Services/DriverAvailabilityService.cs Tests/Services/DriverAvailabilityServiceTests.cs
git commit -m "feat: add GetDriverMonthScheduleAsync for the calendar month view"
```

---

## Task 2: `AdminController.Calendar` month mode

**Files:**
- Modify: `Controllers/AdminController.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`

**Interfaces:**
- Consumes: `IDriverAvailabilityService.GetDriverMonthScheduleAsync`
- Produces: `GET /Admin/Calendar?date=yyyy-MM-dd&view=month`

- [ ] **Step 1: Write failing tests**

Add to `Tests/Controllers/AdminControllerTests.cs`, inside the class:

```csharp
        [Fact]
        public async Task Calendar_WithMonthView_ReturnsMonthScheduleInViewBag()
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
            var result = await controller.Calendar(new DateOnly(2026, 9, 15), "month");

            // Assert
            Assert.IsType<ViewResult>(result);
            var monthSchedule = Assert.IsType<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>>(controller.ViewBag.MonthSchedule);
            Assert.Equal(30, monthSchedule.Count);
            Assert.Equal("month", controller.ViewBag.CalendarView);
        }

        [Fact]
        public async Task Calendar_WithoutViewParam_DefaultsToDayModeUnchanged()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var controller = BuildController(context, withTempData: false);

            // Act
            var result = await controller.Calendar(new DateOnly(2026, 9, 15));

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsType<List<DriverDayScheduleViewModel>>(view.Model);
            Assert.Equal("day", controller.ViewBag.CalendarView);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: FAIL — `Calendar` has no overload accepting a second `view` argument, and `ViewBag.CalendarView`/`ViewBag.MonthSchedule` don't exist yet

- [ ] **Step 3: Update the `Calendar` action**

In `Controllers/AdminController.cs`, replace:

```csharp
        public async Task<IActionResult> Calendar(DateOnly? date)
        {
            var day = date ?? DateOnly.FromDateTime(DateTime.Today);
            var schedule = await _driverAvailabilityService.GetDriverDayScheduleAsync(day);
            ViewBag.SelectedDate = day;
            return View(schedule);
        }
```

with:

```csharp
        public async Task<IActionResult> Calendar(DateOnly? date, string? view = null)
        {
            var day = date ?? DateOnly.FromDateTime(DateTime.Today);
            var calendarView = view == "month" ? "month" : "day";
            ViewBag.SelectedDate = day;
            ViewBag.CalendarView = calendarView;

            if (calendarView == "month")
            {
                ViewBag.MonthSchedule = await _driverAvailabilityService.GetDriverMonthScheduleAsync(day.Year, day.Month);
                return View(new List<DriverDayScheduleViewModel>());
            }

            var schedule = await _driverAvailabilityService.GetDriverDayScheduleAsync(day);
            return View(schedule);
        }
```

Note: `view` defaults to `null` so every existing call site (`controller.Calendar(null)`, `controller.Calendar(requestedDate)`, and the `RedirectToAction(nameof(Calendar), new { date = ... })` calls in `AddTimeOff`) keeps compiling and behaving exactly as before — they all implicitly stay in day mode.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (all previously-passing tests, unmodified, + 2 new ones)

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Controllers/AdminController.cs Tests/Controllers/AdminControllerTests.cs
git commit -m "feat: add month view mode to the Calendar action"
```

---

## Task 3: Month grid view — toggle, partial, CSS

**Files:**
- Modify: `Views/Admin/Calendar.cshtml`
- Create: `Views/Admin/_CalendarMonth.cshtml`
- Modify: `wwwroot/css/site.css`

**Interfaces:**
- Consumes: `ViewBag.CalendarView`, `ViewBag.MonthSchedule` (set by Task 2)

- [ ] **Step 1: Add the Day/Month toggle and month-aware Prev/Next to `Calendar.cshtml`**

In `Views/Admin/Calendar.cshtml`, replace the top `@{ ... }` block:

```html
@model List<RideReady.ViewModels.DriverDayScheduleViewModel>
@{
    ViewData["Title"] = "Driver Calendar";
    ViewData["PageHeading"] = "Driver calendar";
    var selectedDate = (DateOnly)ViewBag.SelectedDate;
    var prevDate = selectedDate.AddDays(-1);
    var nextDate = selectedDate.AddDays(1);
}
```

with:

```html
@model List<RideReady.ViewModels.DriverDayScheduleViewModel>
@{
    ViewData["Title"] = "Driver Calendar";
    ViewData["PageHeading"] = "Driver calendar";
    var selectedDate = (DateOnly)ViewBag.SelectedDate;
    var calendarView = (string)ViewBag.CalendarView;
    var isMonthView = calendarView == "month";
    var firstOfMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1);

    // Day mode steps Prev/Next by one day; month mode steps by one calendar month,
    // anchored to the 1st so "Next" from any day in the month lands on next month's 1st.
    var prevDate = isMonthView ? firstOfMonth.AddMonths(-1) : selectedDate.AddDays(-1);
    var nextDate = isMonthView ? firstOfMonth.AddMonths(1) : selectedDate.AddDays(1);
}
```

Then replace the control-bar `<div class="ride-card mb-3 ...">` block:

```html
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
```

with:

```html
<div class="ride-card mb-3 d-flex justify-content-between align-items-center flex-wrap gap-2">
    <div class="d-flex align-items-center gap-2">
        <div class="btn-group" role="group">
            <a class="btn btn-sm @(isMonthView ? "btn-outline-secondary" : "ride-btn-primary")" asp-action="Calendar" asp-route-date="@selectedDate.ToString("yyyy-MM-dd")" asp-route-view="day">Day</a>
            <a class="btn btn-sm @(isMonthView ? "ride-btn-primary" : "btn-outline-secondary")" asp-action="Calendar" asp-route-date="@selectedDate.ToString("yyyy-MM-dd")" asp-route-view="month">Month</a>
        </div>
        <a class="btn btn-sm btn-outline-secondary" asp-action="Calendar" asp-route-date="@prevDate.ToString("yyyy-MM-dd")" asp-route-view="@calendarView">&larr; Prev</a>
        <form asp-action="Calendar" method="get" class="d-flex align-items-center gap-2 mb-0">
            <input type="hidden" name="view" value="@calendarView" />
            <input type="date" name="date" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" />
            <button type="submit" class="btn btn-sm btn-outline-secondary">Go</button>
        </form>
        <a class="btn btn-sm btn-outline-secondary" asp-action="Calendar" asp-route-date="@nextDate.ToString("yyyy-MM-dd")" asp-route-view="@calendarView">Next &rarr;</a>
    </div>
    <p class="fw-bold mb-0">@(isMonthView ? selectedDate.ToString("yyyy-MM") : selectedDate.ToString("yyyy-MM-dd"))</p>
</div>
```

- [ ] **Step 2: Make the day-view timeline conditional and add the month partial**

In `Views/Admin/Calendar.cshtml`, replace the final block — everything from the last `<div class="ride-card">` to the end of the file:

```html
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
                <form id="timeoff-form-@driver.DriverId" asp-controller="Admin" asp-action="AddTimeOff" method="post" class="d-flex gap-1 mt-2 d-none flex-wrap">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="DriverId" value="@driver.DriverId" />
                    <input type="date" name="StartDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                    <input type="date" name="EndDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                    <input type="text" name="Reason" placeholder="Reason (optional)" class="form-control form-control-sm ride-input" />
                    <button type="submit" class="btn btn-sm ride-btn-primary">Save</button>
                </form>
            </div>
        </div>
    }
</div>
```

with:

```html
@if (isMonthView)
{
    @await Html.PartialAsync("_CalendarMonth", (Dictionary<DateOnly, List<RideReady.ViewModels.DriverDayScheduleViewModel>>)ViewBag.MonthSchedule)
}
else
{
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
                    <form id="timeoff-form-@driver.DriverId" asp-controller="Admin" asp-action="AddTimeOff" method="post" class="d-flex gap-1 mt-2 d-none flex-wrap">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="DriverId" value="@driver.DriverId" />
                        <input type="date" name="StartDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                        <input type="date" name="EndDate" value="@selectedDate.ToString("yyyy-MM-dd")" class="form-control form-control-sm ride-input" required />
                        <input type="text" name="Reason" placeholder="Reason (optional)" class="form-control form-control-sm ride-input" />
                        <button type="submit" class="btn btn-sm ride-btn-primary">Save</button>
                    </form>
                </div>
            </div>
        }
    </div>
}
```

The `@functions { double ToPercent(TimeOnly t) { ... } }` block earlier in the file is untouched — it's still used by the day-mode branch above.

- [ ] **Step 3: Create `Views/Admin/_CalendarMonth.cshtml`**

```html
@model Dictionary<DateOnly, List<RideReady.ViewModels.DriverDayScheduleViewModel>>
@{
    var days = Model.Keys.OrderBy(d => d).ToList();
    var firstDay = days.First();
    var leadingBlanks = (int)firstDay.DayOfWeek;
    var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    var today = DateOnly.FromDateTime(DateTime.Today);
}

@functions {
    string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }
        if (parts.Length == 1)
        {
            return parts[0].Substring(0, 1).ToUpperInvariant();
        }
        return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
    }

    string ChipTitle(RideReady.ViewModels.DriverDayScheduleViewModel driver)
    {
        if (driver.IsOnTimeOff)
        {
            return string.IsNullOrEmpty(driver.TimeOffReason)
                ? $"{driver.DriverName} - on time off"
                : $"{driver.DriverName} - on time off: {driver.TimeOffReason}";
        }

        var trips = driver.Blocks.Select(b => $"{b.Start:HH:mm}-{b.End:HH:mm} {b.BookingReference}");
        return $"{driver.DriverName} - " + string.Join("\n", trips);
    }
}

<div class="ride-card">
    <div class="ride-month-grid">
        @foreach (var name in dayNames)
        {
            <div class="ride-month-dow">@name</div>
        }

        @for (var i = 0; i < leadingBlanks; i++)
        {
            <div class="ride-month-day ride-month-day--blank"></div>
        }

        @foreach (var date in days)
        {
            var driversForDay = Model[date].Where(d => d.IsOnTimeOff || d.Blocks.Any()).ToList();
            var isToday = date == today;
            <div class="ride-month-day @(isToday ? "today" : "")">
                <a class="ride-month-daynum" asp-controller="Admin" asp-action="Calendar" asp-route-date="@date.ToString("yyyy-MM-dd")" asp-route-view="day">@date.Day</a>
                <div class="ride-month-chips">
                    @foreach (var driver in driversForDay)
                    {
                        <span class="ride-month-chip @(driver.IsOnTimeOff ? "ride-month-chip--timeoff" : "")" title="@ChipTitle(driver)">@Initials(driver.DriverName)</span>
                    }
                </div>
            </div>
        }
    </div>
</div>
```

- [ ] **Step 4: Add month-grid CSS**

Append to `wwwroot/css/site.css`:

```css
.ride-month-grid {
    display: grid;
    grid-template-columns: repeat(7, 1fr);
    gap: 6px;
}

.ride-month-dow {
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--ride-text-muted);
    text-align: center;
    padding-bottom: 4px;
}

.ride-month-day {
    background: var(--ride-bg-2);
    border: 1px solid var(--ride-border);
    border-radius: 10px;
    min-height: 84px;
    padding: 6px;
}

.ride-month-day--blank {
    background: transparent;
    border: none;
}

.ride-month-day.today {
    border-color: var(--ride-vivid-green);
    border-width: 2px;
}

.ride-month-daynum {
    font-weight: 700;
    color: var(--ride-text);
    text-decoration: none;
    display: inline-block;
}

.ride-month-daynum:hover {
    color: var(--ride-mid-green);
}

.ride-month-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    margin-top: 6px;
}

.ride-month-chip {
    background: var(--ride-bg-4);
    color: var(--ride-mid-green);
    border: 1px solid var(--ride-bright-green);
    border-radius: 999px;
    font-size: 0.62rem;
    font-weight: 700;
    padding: 1px 6px;
}

.ride-month-chip--timeoff {
    background: repeating-linear-gradient(45deg, #e5e5e5, #e5e5e5 3px, #d4d4d4 3px, #d4d4d4 6px);
    color: var(--ride-text-muted);
    border-color: #d4d4d4;
}
```

- [ ] **Step 5: Verify the project builds (Razor views compile as part of build)**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all tests — this task touches no test files, so the count should be unchanged from Task 2)

- [ ] **Step 7: Commit**

```bash
git add Views/Admin/Calendar.cshtml Views/Admin/_CalendarMonth.cshtml wwwroot/css/site.css
git commit -m "feat: add month grid view to the driver calendar"
```

---

## Task 4: Live verification

**Files:** None (verification task)

- [ ] **Step 1: Rebuild and restart the local dev stack**

If a local Docker stack is running (see `run.bat`), rebuild the image and restart the containers so the new code is live. Wait for `/health` to report healthy.

- [ ] **Step 2: Run the full test suite one more time**

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 3: Manual smoke test against the running app**

- Log into `/AdminAuth/Login`, visit `/Admin/Calendar` — confirm it still loads in Day mode by default, unchanged from before this feature
- Click "Month" — confirm a month grid renders for the current month, with a "Day"/"Month" toggle and Prev/Next now stepping by month
- Assign a driver to a booking on some date this month, then check that date's month-grid cell shows that driver's initials as a chip
- Hover the chip — confirm the tooltip shows the driver's full name and trip time/reference
- Add time off for a driver on some date this month via the Day view, then check Month view shows a hatched chip for that driver on that date, with the reason in the tooltip
- Click a date number in the month grid — confirm it navigates to Day view for that date
- Click "Day" from month view, then "Month" again — confirm the toggle round-trips correctly and Prev/Next in Day mode still step by single days, unaffected by having visited Month mode

- [ ] **Step 4: Note completion**

No commit needed for this task — it's verification only. If the manual smoke test surfaces a bug, fix it as a new commit and re-run `dotnet test` before considering the plan complete.
