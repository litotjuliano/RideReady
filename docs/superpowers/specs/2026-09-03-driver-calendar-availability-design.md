# Driver Calendar & Availability — Design Specification

**Project:** RideReady — Admin Driver Calendar & Availability Checking
**Date:** 2026-09-03
**Version:** 1.0.0
**Status:** Ready for Implementation
**Parent spec:** `docs/superpowers/specs/2026-09-01-ride-booking-system-design.md` (§3.2 "Calendar View" and "driver schedule view" were described there but never built — this spec supersedes those two bullet points with a concrete design)

---

## 1. Summary

Admins currently assign drivers to bookings from a flat dashboard list (`Admin/Index`) with no visibility into whether a driver is already busy at that time, and no way to see a driver's schedule at a glance. This adds:

1. A new **Calendar page** (`Admin/Calendar`) showing, for a chosen day, one timeline row per active driver with their booked trips and time-off blocked out.
2. **Availability checking** shared between the Calendar page and the existing driver-assignment dropdown on `Admin/Index`, so admins see conflicts before assigning, not just after (the existing hard double-booking guard in `AssignDriverAsync` remains as the final safety net).
3. **Manual time-off**, admin-entered, full-day granularity, blocking the driver on the calendar and in the assignment dropdown.

**Explicitly out of scope** (deferred to future work):
- Live GPS driver-location tracking (the existing unused `DriverLocation` table / browser-geolocation design from the parent spec §8.2) — a separate future project
- Driver self-service time-off requests (admin-only for now)
- Editing/deleting time-off entries (add-only in this version)
- Time-range-level time-off (only full-day blocks)
- Week/month calendar views (day view only)

---

## 2. Architecture decision

New service: **`IDriverAvailabilityService`**, separate from `IDriverAssignmentService`.

Rejected alternatives:
- Extending `DriverAssignmentService` directly — that service already owns dashboard queries, assignment, and status transitions; adding calendar/time-off logic would grow it further and blur its responsibility.
- Computing availability ad-hoc in controllers/views — would duplicate the busy-window logic between the Calendar page and the Assign dropdown, and couldn't be unit tested in isolation.

`IDriverAvailabilityService` is the single source of truth both consumers query.

---

## 3. Data model

### New table: `DriverTimeOff`

```csharp
// Models/DriverTimeOff.cs
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
```

- Full-day granularity: a row blocks the driver for every pickup on `StartDate..EndDate` inclusive, regardless of time of day.
- No changes to `Booking`, `DriverAssignment`, or `BookingQuote`.
- Add `DbSet<DriverTimeOff> DriverTimeOffs` to `RideReadyDbContext`, plus a migration.

---

## 4. Availability computation

For a given date, for each **active** driver (`IsActive == true`):

**Busy blocks** — every `DriverAssignment` for that driver whose `Booking.PickupDate` matches, where `Booking.Status` is not `Cancelled`, `No_Show`, or `Completed`, and `AssignmentStatus != "Rejected"`, rendered as a `(start, end)` window:
- `start = Booking.PickupTime`
- `end = start + Booking.Quote.DurationHours` if the booking has a real quote (`Quote != null && Quote.DurationHours > 0`)
- otherwise `end = start + 2h` (fixed fallback — covers manual-fare bookings where the quote was zeroed out per the existing "Price not set" flow)

**Time-off** — any `DriverTimeOff` row whose `[StartDate, EndDate]` includes the date → driver is unavailable for the entire day, shown as one full-day block, no time windows needed.

**Free** — everything else within the displayed window (6 AM–midnight, matching the existing booking time-slot rule in the parent spec §5.2).

### `IDriverAvailabilityService`

```csharp
public interface IDriverAvailabilityService
{
    Task<List<DriverDayScheduleViewModel>> GetDriverDayScheduleAsync(DateOnly date);
    Task<(bool IsAvailable, string? ConflictReason)> IsDriverAvailableAsync(int driverId, DateOnly date, TimeOnly time);
}
```

- `GetDriverDayScheduleAsync` — one entry per active driver, each with its busy blocks + a time-off flag. Powers the Calendar page.
- `IsDriverAvailableAsync` — single driver/time check, reusing the same block logic, returning a human-readable conflict reason (e.g. `"busy: RR-9RLI63YS 14:00–16:00"` or `"on time off"`) for the Assign dropdown.

The existing hard double-booking guard in `DriverAssignmentService.AssignDriverAsync` (exact pickup-time match) is unchanged and remains the final server-side safety net. The new service is a *pre-assignment* warning layer — an admin can still deliberately assign a driver flagged as "busy" (e.g. an earlier trip running long); only an exact-time conflict is hard-blocked, as today.

### `DriverDayScheduleViewModel`

```csharp
public class DriverDayScheduleViewModel
{
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public bool IsOnTimeOff { get; set; }
    public string? TimeOffReason { get; set; }
    public List<DriverScheduleBlockViewModel> Blocks { get; set; } = new();
}

public class DriverScheduleBlockViewModel
{
    public int BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
```

---

## 5. Calendar page (`Admin/Calendar`)

New nav item alongside "Dispatch" and "Drivers" in `_AdminHeader.cshtml`.

**Layout:**
- Date picker at top (defaults to today), with Prev/Next day links (`?date=yyyy-MM-dd`)
- One horizontal row per active driver:
  - Driver name + vehicle type on the left
  - A timeline strip spanning 6 AM–midnight with:
    - Green-tinted blocks for booked trips, labeled with time range + booking reference + pickup→destination
    - Gray-hatched block spanning the full strip for time-off days
  - Clicking a booked block links to `Admin/Index` (existing dashboard) — no new detail view
- All active drivers always shown (no filter/dropdown in this version)
- A small "Add time-off" form per driver row: date range + optional reason, posting to `AdminController.AddTimeOff`

Reuses the existing visual style (`ride-card`, pill badges, green palette from parent spec §20) — no new UI kit.

**Controller:**
```csharp
// AdminController.cs — new actions
public async Task<IActionResult> Calendar(DateOnly? date)
{
    var day = date ?? DateOnly.FromDateTime(DateTime.Today);
    var schedule = await _driverAvailabilityService.GetDriverDayScheduleAsync(day);
    ViewBag.SelectedDate = day;
    return View(schedule);
}

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

---

## 6. Assign dropdown integration (`Admin/Index`)

The existing dropdown already filters to matching-vehicle-type drivers (falling back to all drivers if none match). On top of that:

- `AdminController.Index` calls `IDriverAvailabilityService.IsDriverAvailableAsync` per driver per booking when building the dashboard view model (once per page load, server-side — not per-request client-side)
- Each `<option>` for a busy/time-off driver gets a suffix and is visually de-emphasized, e.g.:
  - `Ahmad (Car) — busy: RR-9RLI63YS 14:00–16:00`
  - `Ahmad (Car) — on time off`
- Busy/time-off drivers are **not removed** from the list — admin can still force-assign, accepting the risk. Only the exact-time hard guard in `AssignDriverAsync` blocks outright.

`AdminBookingListItemViewModel` (or the dropdown-building logic in the view/controller) needs the per-driver availability annotations; exact shape (e.g. a `Dictionary<int, string?>` of driver-id → conflict-reason passed via `ViewBag`, similar to today's `ViewBag.ActiveDrivers`) is an implementation detail for the planning phase.

---

## 7. Time-off management rules

- **Who:** Admin only (no driver self-service in this version)
- **Granularity:** Full calendar day(s) only
- **Conflict handling:** `AddTimeOffAsync` rejects the request if any active assignment (`Booking.Status` not `Cancelled`/`No_Show`/`Completed`, `AssignmentStatus != "Rejected"`) exists for that driver with `PickupDate` anywhere in `[StartDate, EndDate]`, throwing `InvalidOperationException` naming the conflicting booking reference — same pattern as the existing double-booking guard in `AssignDriverAsync`.
- **Validation:** `EndDate >= StartDate` required.
- **Editing/deletion:** Not included in this version — a mistaken entry requires a direct DB fix. Add-only.

---

## 8. Testing

- **`DriverAvailabilityServiceTests`**: busy-window calculation with real quote duration; fallback 2h duration when quote is zeroed/absent; time-off blocking a full day; free-driver case (no blocks); `IsDriverAvailableAsync` conflict-reason string content for both busy and time-off cases.
- **`AddTimeOff` tests**: success case; conflict-with-active-assignment rejection (asserting the booking reference appears in the message); invalid date range (`EndDate < StartDate`).
- **Controller tests** for `AdminController.Calendar` (renders the day schedule for a given/default date) and `AdminController.AddTimeOff` (success/error TempData paths), following the existing pattern in `Tests/Controllers/AdminControllerTests.cs`.
- No new external dependencies — no Google Calendar API changes, no live GPS, stays entirely within the existing PostgreSQL + Bootstrap 5 stack.

---

## 9. Non-goals / future work

- Live GPS driver-location tracking, using the existing unused `DriverLocation` table and the browser-geolocation approach described in the parent spec §8.2 — deferred as a separate project.
- Driver self-service time-off requests via the Driver Portal.
- Editing or deleting time-off entries through the UI.
- Time-range-level (not full-day) time-off.
- Week/month calendar views — day view only for now.
