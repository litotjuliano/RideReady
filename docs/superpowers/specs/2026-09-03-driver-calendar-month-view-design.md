# Driver Calendar — Month View — Design Specification

**Project:** RideReady — Admin Driver Calendar Month View
**Date:** 2026-09-03
**Version:** 1.0.0
**Status:** Ready for Implementation
**Parent spec:** `docs/superpowers/specs/2026-09-03-driver-calendar-availability-design.md` (built the day-view `Admin/Calendar` page and `IDriverAvailabilityService` this spec extends)

---

## 1. Summary

The day-view `Admin/Calendar` page (one horizontal timeline row per driver, for a single date) shipped as the first version of the driver calendar. Admins now want to see a whole month at a glance and drill into any date's driver assignments from there. This adds a **Month view** to the same Calendar page, toggled alongside the existing Day view, showing each active driver's status per day as a small initial chip, with a native tooltip on hover and a click-through to the existing Day view for that date.

No new data-off/assignment behavior is introduced — this is a read/navigation layer on top of the existing `IDriverAvailabilityService` data.

---

## 2. Decisions from brainstorming

- **Clicking a date** in month view navigates to the existing Day view for that date (reuses everything already built there — no new detail UI).
- **Chip hover** shows a native browser tooltip (the `title` attribute, same mechanism as the Day view's booked blocks) with the driver's full name and status detail — not a custom styled popover.
- **View switching** is a "Day | Month" toggle next to the existing Prev/date-picker/Next controls on the same `Admin/Calendar` page — one URL, a `view` query param, not a separate page/route.
- **Multiple bookings, same driver, same day**: still just one chip for that driver; the tooltip lists each trip (time range + reference) stacked, rather than one chip per booking.
- **Cell content style**: driver-initial chips (e.g. "AS", "SA"), styled as pill chips matching the app's existing badge look — chosen over a plain count badge (too little detail) and a mini per-driver status-bar stack (denser, but less scannable than initials for a small roster). See mockup exploration for the rejected alternatives.
- **Month-view scope is read-only/navigation only** — no "+ Time off" form in month cells (too dense to fit 6+ drivers' forms into a day cell). Time-off entry stays a Day-view-only action, reached by clicking through.
- **Adjacent-month overflow days**: leading/trailing cells before the 1st and after the last day of the month are left blank, not populated with the neighboring month's dates. Simplest option; avoids extra date-math and ambiguity about whether those cells are interactive.

---

## 3. Data layer

### New method: `IDriverAvailabilityService.GetDriverMonthScheduleAsync(int year, int month)`

```csharp
Task<Dictionary<DateOnly, List<DriverDayScheduleViewModel>>> GetDriverMonthScheduleAsync(int year, int month);
```

Returns one entry per day of the given month, each holding the same `List<DriverDayScheduleViewModel>` shape the Day view already uses (active drivers with their `Blocks` and time-off flag/reason for that specific day) — **no new view models**, since a month is just many days, each already shaped exactly like today's single-day data.

**Why not loop `GetDriverDayScheduleAsync` once per day of the month:** that would issue ~3 queries × ~30 days ≈ 90 queries per page load — the same N-query anti-pattern already found and fixed in `AdminController.Index()` (see the parent spec's implementation history). Instead, `GetDriverMonthScheduleAsync` issues a small, fixed number of queries for the whole month:

1. Active drivers (as today).
2. `DriverAssignments` → `Booking` → `Quote`, filtered through the existing `ActiveAssignments()` predicate, with `Booking.PickupDate` between the first and last day of the month (inclusive) — one query instead of one per day.
3. `DriverTimeOffs` overlapping the month range (`StartDate <= monthEnd && EndDate >= monthStart`) — one query instead of one per day.

Then, in memory, for each day in the month × each active driver, build a `DriverDayScheduleViewModel` exactly the way `GetDriverDayScheduleAsync` already does per-day — reusing the same `ToBlock` block-construction logic (including its midnight-clamp fix). The per-day/per-driver construction step should be factored into a small shared private helper so `GetDriverDayScheduleAsync` and `GetDriverMonthScheduleAsync` both call it, rather than duplicating that logic — consistent with how `ActiveAssignments()` was already extracted to avoid a third copy of the assignment-filter predicate.

`AdminController.Index()`'s own date-grouped optimization (built during the parent feature) is **not** changed to use this new method — `Index()`'s bookings can span an unbounded date range (no month boundary), so its existing per-distinct-date loop over `GetDriverDayScheduleAsync` stays as-is. This is additive, not a refactor of existing code.

---

## 4. UI

### `Admin/Calendar` — view toggle

- `AdminController.Calendar(DateOnly? date, string? view)` — `view` is `"day"` (default, backward compatible with the existing URL shape) or `"month"`.
- A "Day | Month" toggle rendered next to the existing Prev / date-input / Next controls, each option linking to the same action with `view` set accordingly, preserving the current `date`.
- **Day mode**: unchanged — Prev/Next step by one day, existing timeline renders exactly as today.
- **Month mode**: Prev/Next step by one calendar month (first-of-month arithmetic on the current `date`); the existing `<input type="date">` stays as the date control — picking any day within a different month jumps the grid to that day's month.

### Month grid rendering — new partial `Views/Admin/_CalendarMonth.cshtml`

- Standard 7-column (Sun–Sat) week grid, one row per week needed to cover the month.
- Each in-month day cell shows the day number, then one pill chip per driver who has any booking or time-off that day:
  - Busy driver: initials in the existing green pill style (matching `ride-bg-4`/`ride-mid-green`/`ride-bright-green` tokens from the Day view's booked blocks).
  - On-time-off driver: initials in the existing hatched/gray style (matching the Day view's time-off block).
  - `title` attribute on each chip: driver's full name + status detail — `"Ah Seng — busy: RR-38PPIBHM 10:00–12:00"` for a single trip, each trip's time range + reference newline-stacked for multiple trips, or `"Chong Wei Ming — on time off"` (plus reason if set).
  - The day number itself links to `Admin/Calendar?date=<that day>&view=day`.
- Leading/trailing cells outside the month are rendered blank (no day number, no chips, not a link).
- Kept as its own file (isolated from `Calendar.cshtml`'s existing day-view markup) so each view stays focused and the diff for this feature is easy to review in isolation.

### Driver initials

Computed from `DriverName`: the first letter of each of the first two whitespace-separated words, uppercased (e.g. "Ah Seng" → "AS"). A single-word name uses just that word's first letter (e.g. "Cher" → "C"). A small helper, not stored on the `Driver` model. Collisions between two drivers sharing initials are acceptable for this roster size; the tooltip's full name disambiguates on hover, and this isn't a new problem introduced by month view (the Day view already renders full names, so there's no existing initials-uniqueness expectation to break).

---

## 5. Testing

- **`DriverAvailabilityServiceTests`**: `GetDriverMonthScheduleAsync` covering — a booking on the first/last day of the month (boundary correctness), a driver with two bookings on the same day (single day entry, both blocks present), a `DriverTimeOff` row that starts before the month and ends inside it (and the reverse: starts inside, ends after) to confirm month-boundary overlap handling, and a month with no active drivers/no data returning an empty-but-well-formed structure.
- **`AdminControllerTests`**: `Calendar(date, view: "month")` returns the month dictionary; `Calendar(date, view: "day")` (or omitted) is unchanged from today's behavior.
- **No Razor-view unit tests** — consistent with how the Day view was verified (`dotnet build` for Razor compilation + a live manual smoke test against the running app), not introducing a new testing pattern for this feature.

---

## 6. Non-goals

- No time-off entry from month view (Day-view-only, per the brainstorming decision).
- No year view, no week view — only Day and Month.
- No visual distinction for adjacent-month overflow days (they're simply blank).
- No change to `AdminController.Index()`'s existing date-grouped query optimization.
