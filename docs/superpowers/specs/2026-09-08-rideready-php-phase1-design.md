# RideReady PHP (Laravel) — Phase 1 Design Specification

**Project:** Clone RideReady into a PHP/Laravel application, in a new standalone folder `RideReady - PHP`
**Date:** 2026-09-08
**Version:** 1.0.0
**Status:** Ready for Implementation
**Scope:** Phase 1 of an incremental port. Later phases (driver assignment, admin status updates, driver portal, map integration) are out of scope here and get their own specs — see §8.

---

## 1. Summary

The existing RideReady app (`App/`) is an ASP.NET Core MVC + EF Core system on Postgres, with customer booking, driver portal, admin panel, and notification/integration modules. This spec covers a new, independent Laravel 11 application — `RideReady - PHP` — that replicates only the **core** slice of that system:

1. A guest customer can submit a booking request (no login) and get a fare quote + reference number.
2. Submitting a booking sends a WhatsApp notice to the operator/admin.
3. An admin can log in (Laravel's built-in auth) and see a read-only list of all bookings.

Everything else in the original app (driver assignment, status transitions, driver portal, email, calendar sync, real Google Maps) is deliberately deferred to later phases (§8), so this phase stays small enough to implement and review as one unit.

## 2. Architecture

- **Framework:** Laravel 11, standard MVC — routes → controllers → Eloquent models, Blade views for server-rendered HTML (mirrors the original's server-rendered Razor Views, not an API+SPA split).
- **Database:** MySQL (chosen over matching the original's Postgres, since there's no shared-infra requirement).
- **Location:** New folder `RideReady - PHP/` at the repo root, sibling to `App/` — a fully standalone Laravel project with its own `composer.json`, `.env`, and `artisan`. It does not share code, config, or a database with the .NET app.
- **Auth:** Laravel's built-in `Auth` facade with a dedicated `admin` guard, backed by an `admins` table (bcrypt-hashed passwords via `Hash::make`). No customer accounts in this phase — customer booking stays guest/anonymous, matching the original.

## 3. Data Model

Five tables/models, a subset of the original's schema — only what phase 1 needs:

| Model | Key fields | Notes |
|---|---|---|
| `Customer` | name, phone (unique), email, timestamps | `hasMany` bookings |
| `Booking` | booking_reference (unique), customer_id (FK), pickup_location, destination, pickup_date, pickup_time, passengers, bags, requested_vehicle_type, notes, status (default `New`), timestamps | `belongsTo` customer, `hasOne` quote |
| `BookingQuote` | booking_id (FK, unique), base_fare, distance_km, distance_charge, duration_hours, time_charge, passenger_surcharge, luggage_fee, subtotal, service_tax, total_estimated_fare, actual_fare (nullable), payment_method, created_at | `belongsTo` booking |
| `PricingSetting` | vehicle_type, base_fare, per_km_rate, per_hour_rate, first_km_distance, first_km_charge (nullable), passenger_surcharge (nullable), luggage_fee_per_extra (default 5), service_tax_percent, is_active, timestamps | Seeded with one active row per vehicle type (Car, Van, Bus) |
| `Admin` | username (unique), password (bcrypt hash), timestamps | Seeded with one default admin from `.env` (`ADMIN_USERNAME` / `ADMIN_PASSWORD`) |

No `Driver`, `DriverAssignment`, `BookingStatusHistory`, or `Notification` tables in this phase — those belong to later phases (§8).

## 4. Customer Booking Flow (guest, no login)

**`GET /booking`** renders a form with:

- Full name (required, 3–100 chars)
- Phone (required, Malaysian format: `+60XXXXXXXXX` or `01X-XXXXXXXX`, same regex as the original)
- Email (required, valid email)
- Pickup location / destination (required, 5–255 chars each)
- Pickup date (required, must resolve to a future date/time on submit)
- Pickup time (required, must fall between 06:00 and 23:59 — bookings are not accepted from midnight to 6AM)
- Passengers (1–8), bags (0–10)
- Vehicle type (`Car` | `Van` | `Bus`)
- Notes (optional, ≤500 chars)
- Payment method (`Pay_at_Pickup` | `Bank_Transfer`)
- Terms acceptance (must be checked)

All validated via a Laravel Form Request.

**`POST /booking`** — a `BookingService` class runs inside a DB transaction:

1. Reject if the resolved pickup date/time is in the past, or the pickup hour falls outside 6AM–midnight.
2. Find the `Customer` by phone, or create one from the submitted name/phone/email.
3. Create the `Booking` with a generated unique reference: `RR-` + 8 random uppercase-alphanumeric characters.
4. Calculate a quote:
   - Look up the active `PricingSetting` row for the requested vehicle type.
   - Get distance (km) and duration (hours) from a `MockDistanceService` — a stand-in that returns a plausible pseudo-random estimate (no real mapping API in this phase; see §8).
   - Apply the original's fare formula: `base_fare + distance_charge + time_charge + passenger_surcharge + luggage_fee` as the subtotal, where:
     - `distance_charge` = `first_km_charge` if distance ≤ `first_km_distance`, else `first_km_charge + (distance - first_km_distance) * per_km_rate`
     - `time_charge` = `duration_hours * per_hour_rate`
     - `passenger_surcharge` = `max(0, passengers - 1) * passenger_surcharge_rate`
     - `luggage_fee` = `max(0, bags - 2) * luggage_fee_per_extra`
   - `service_tax` = `subtotal * (service_tax_percent / 100)`; `total_estimated_fare` = `subtotal + service_tax`.
   - If no active `PricingSetting` exists for the requested vehicle type, don't block the booking — create it with a zeroed-out quote instead (logged as a warning), matching the original's fallback behavior. The customer never sees quote numbers either way in this phase; the confirmation page only shows the reference.
5. Commit the transaction.

After a successful commit, call `WhatsAppService::sendOperatorBookingNotice($booking)`:

- Sends `"New booking {reference}: {pickup} -> {destination} on {date} {time}."` to the configured operator phone via the WhatsApp Cloud API (`messaging_product: whatsapp`, `type: text`, Bearer token auth), phone normalized to `60XXXXXXXXXX` format.
- Wrapped in try/catch: on failure, log the API's full response body (not just the HTTP status — matching the recent fix in the original app) and continue. A WhatsApp failure never blocks the booking or fails the customer's request.
- No notification-log database table in this phase (see §8) — failures/successes go to Laravel's application log only.

**`GET /booking/confirmation`** shows the booking reference (via flashed session data, mirroring the original's `TempData` pattern) and redirects back to the form if accessed directly without a just-created booking.

### Config (`.env`)

```
WHATSAPP_API_URL=
WHATSAPP_ACCESS_TOKEN=
WHATSAPP_PHONE_NUMBER_ID=
WHATSAPP_OPERATOR_PHONE=
```

## 5. Admin Login + Dashboard

- **`GET /admin/login`** — username + password form.
- **`POST /admin/login`** — `Auth::guard('admin')->attempt(['username' => ..., 'password' => ...])`. On failure, re-render the form with one generic error: "Invalid username or password" (no distinction between unknown username and wrong password).
- **`POST /admin/logout`** — `Auth::guard('admin')->logout()`, invalidate the session, redirect to login.
- **`GET /admin`** — protected by an `auth:admin` middleware; unauthenticated requests redirect to `/admin/login`. Renders a read-only table of all bookings: reference, customer name/phone, pickup → destination, date/time, passengers/bags, vehicle type, status, estimated fare. No assign-driver, edit, or status-change actions in this phase.
- A database seeder creates exactly one admin account from `ADMIN_USERNAME` / `ADMIN_PASSWORD` env vars, hashing the password at seed time.

## 6. Error Handling

| Case | Behavior |
|---|---|
| Booking form validation failure | Redisplay form with field-level errors and old input (Laravel default) |
| Past pickup date/time, or outside 6AM–midnight | Redisplay form with a single top-level error message |
| No active `PricingSetting` for requested vehicle type | Booking still created, with a zeroed quote; warning logged |
| WhatsApp send failure | Logged with the API's error body; booking request still succeeds |
| Admin login failure | Generic "Invalid username or password" |
| Unauthenticated access to `/admin` | Redirect to `/admin/login` |

## 7. Testing

- **Feature tests** (Laravel HTTP test helpers):
  - Booking creation succeeds and returns a reference.
  - Past-date booking is rejected.
  - Booking outside 6AM–midnight is rejected.
  - Booking with no matching active `PricingSetting` still succeeds, with a zeroed quote.
  - Admin login succeeds with correct credentials, fails with incorrect ones.
  - `/admin` redirects to `/admin/login` when unauthenticated; loads the bookings list when authenticated.
- **Unit tests**:
  - Fare calculation formula in isolation (mirrors the original's `BookingService.GetQuoteAsync` math) across a few representative inputs.
  - WhatsApp phone-normalization logic (`0123456789` → `60123456789`, etc.) and payload shape.
  - WhatsApp send failure handling, with the HTTP call faked via `Http::fake()` so tests never hit the real API.

## 8. Non-Goals (deferred to later phases)

This phase intentionally excludes the rest of the original app's workflow, captured here so it isn't lost:

- **Phase 2 — Driver assignment & status:** `Driver` model, driver availability, admin dashboard gains an "assign driver" action (admin manually reconfirms with the driver by phone, then assigns in the dashboard), admin sends the finalized price to the customer via WhatsApp, admin updates booking status on the dashboard.
- **Phase 3 — Driver portal:** Driver login, drivers update job status themselves, which auto-sends WhatsApp updates (customer and/or operator).
- **Phase 4 — Map integration:** Real Google Maps distance/duration lookup (replacing `MockDistanceService`), plus a map location on/off toggle flag for the admin dashboard.
- No email notifications, no calendar sync, no notification-log database table, no customer accounts/login.
