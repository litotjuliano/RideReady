# Fare and Trip Status WhatsApp Notifications — Design Specification

**Project:** RideReady — notify the customer when a fare is set, and notify the customer + operator as a driver progresses through a trip
**Date:** 2026-09-08
**Version:** 1.0.0
**Status:** Ready for Implementation
**Related work:** `docs/superpowers/specs/2026-09-04-operator-whatsapp-on-booking-created-design.md` (this reuses the exact same `IWhatsAppSender`/`SendAndLogAsync` pattern, no new abstractions)

---

## 1. Summary

A request to implement a full booking-lifecycle workflow (customer books → admin assigns a driver → admin prices it → customer confirms → status updates → live location tracking) turned out to already exist almost entirely in the `.NET` app (`App/`): booking creation already WhatsApps the operator, the admin dashboard already shows driver availability and lets the admin assign a driver, the admin can already update booking status (including "Confirmed"), and the driver portal already has a live location on/off toggle.

Two genuine gaps remain, both addressed here:

1. When an admin sets a booking's fare, the customer is never told the price.
2. When a driver updates their trip status (accepts a job, picks up, in transit, dropped off, completed), nothing is sent to the customer or operator except a customer email on acceptance and on completion — no WhatsApp at all, and no notification of any kind for "picked up" or "in transit".

## 2. Architecture

Both gaps are additive changes to the existing `NotificationService` (`App/Services/NotificationService.cs`), following the pattern already used for every notification in this app: build a message, call `SendAndLogAsync(...)`, which sends via the injected `IWhatsAppSender`/`IEmailSender` and logs a `Notifications` row (`Sent`/`Failed`) without ever throwing back to the caller. No new services, no new tables, no new abstractions.

## 3. Price Notification (fixes gap: admin sets fare, customer isn't told)

New method:

```csharp
Task SendPriceSetNotificationAsync(int bookingId);
```

- Loads the booking (with `Customer` and `Quote`).
- Sends one WhatsApp to the customer's phone:
  ```
  Hi {CustomerName}, the fare for your RideReady booking {BookingReference} is RM{TotalEstimatedFare:F2} ({PaymentMethodText}). Thank you!
  ```
  where `PaymentMethodText` maps `Quote.PaymentMethod` (`Pay_at_Pickup` → "Pay at Pickup", `Bank_Transfer` → "Bank Transfer").
- **Trigger:** called from `AdminController.SetFare`, immediately after `BookingService.SetManualFareAsync` succeeds — so every time an admin saves/edits a fare, the customer is notified automatically. No new UI; reuses the existing "Set Fare" form.

## 4. Driver Trip Status Notifications (fixes gap: driver updates aren't announced)

### 4.1 New: interim statuses (Picked_Up, In_Transit, Dropped_Off)

New method:

```csharp
Task SendTripStatusUpdateNotificationAsync(int bookingId, string newStatus);
```

For `newStatus` in `{"Picked_Up", "In_Transit", "Dropped_Off"}`, sends one WhatsApp to the customer and one to the operator:

| Status | Customer message | Operator message |
|---|---|---|
| Picked_Up | "Your RideReady driver has picked you up for booking {Ref}. Enjoy your ride!" | "Booking {Ref}: driver has picked up the customer." |
| In_Transit | "You're on your way! Booking {Ref} is now in transit." | "Booking {Ref} is now in transit." |
| Dropped_Off | "You've arrived! Thanks for riding with RideReady (booking {Ref})." | "Booking {Ref}: customer dropped off." |

**Trigger:** called from `DriverController.UpdateStatus`, right after `DriverPortalService.UpdateTripStatusAsync` succeeds, for these three status values.

### 4.2 Extend: Completed

`NotificationService.SendBookingCompletedNotificationAsync` already emails the customer when *admin* marks a booking Completed (`AdminController.UpdateStatus`). Extend it to also WhatsApp the customer (same wording as the existing email: "Thanks for riding with RideReady! Your trip {Ref} is complete.") and WhatsApp the operator ("Booking {Ref} completed.").

**Trigger:** also call this method from `DriverController.UpdateStatus` when `newStatus == "Completed"` — today driver-triggered completion notifies no one at all; this makes admin- and driver-triggered completion behave identically.

### 4.3 Extend: Accepted

`NotificationService.SendDriverAcceptedNotificationAsync` already emails the customer when a driver accepts (`DriverController.Accept`). Extend it to also WhatsApp the customer (same wording as the existing email) and add a new WhatsApp to the operator ("Driver accepted booking {Ref}.") — today the operator isn't told when a driver accepts at all.

**Trigger:** no change needed — already called from `DriverController.Accept`.

### 4.4 Out of scope

`DriverController.Reject` is unchanged. No notification requested for it; the admin already sees rejected assignments need reassignment via the existing dashboard.

## 5. Error Handling

Unchanged from the existing pattern: every send goes through `SendAndLogAsync`, which catches exceptions from the underlying `IWhatsAppSender`/`IEmailSender` call, records `DeliveryStatus = Failed` with the error message, and never propagates the exception. A WhatsApp outage never blocks `SetFare` from saving a price or blocks a driver from updating trip status.

## 6. Testing

- `NotificationServiceTests` (using the existing `FakeWhatsAppSender`/`FakeEmailSender` test doubles):
  - `SendPriceSetNotificationAsync` sends a WhatsApp to the customer with the correct fare and payment method text.
  - `SendTripStatusUpdateNotificationAsync` sends WhatsApp to both customer and operator for each of Picked_Up/In_Transit/Dropped_Off, with status-appropriate wording.
  - `SendBookingCompletedNotificationAsync` — existing email assertion stays; add assertions for the new customer + operator WhatsApp sends.
  - `SendDriverAcceptedNotificationAsync` — existing email assertion stays; add assertions for the new customer WhatsApp send and the new operator WhatsApp send.
- Controller-level tests:
  - `AdminController.SetFare`: saving a fare triggers `SendPriceSetNotificationAsync`.
  - `DriverController.UpdateStatus`: each of Picked_Up/In_Transit/Dropped_Off triggers `SendTripStatusUpdateNotificationAsync`; `Completed` triggers `SendBookingCompletedNotificationAsync`.
- No new test infrastructure — reuses the existing fakes, matching how every prior WhatsApp feature in this repo was tested.

## 7. Non-Goals

- No changes to booking creation, driver assignment, or the "Confirmed" status flow — all already work as-is (see §1).
- No changes to the live location on/off toggle — already implemented (`driver-location.js`, `DriverController.ReportLocation`).
- No changes to `DriverController.Reject`.
- No new database tables or schema changes — reuses the existing `Notifications` table.
