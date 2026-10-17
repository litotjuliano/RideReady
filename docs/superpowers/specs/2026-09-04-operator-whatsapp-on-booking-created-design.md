# Operator WhatsApp Notice on Booking Created — Design Specification

**Project:** RideReady — notify the operator/admin via WhatsApp when a customer submits a booking
**Date:** 2026-09-04
**Version:** 1.0.0
**Status:** Ready for Implementation
**Parent work:** `docs/superpowers/specs/2026-09-04-whatsapp-real-delivery-design.md` (got the driver-assigned WhatsApp path actually delivering; this reuses the same real credentials and `IWhatsAppSender`)

---

## 1. Summary

`NotificationService.SendBookingCreatedNotificationAsync` currently notifies the operator only by email (plus a calendar event) when a customer submits a new booking. This adds a WhatsApp message to the operator as well, mirroring the exact pattern `SendDriverAssignedNotificationAsync` already uses to WhatsApp a driver — no new abstractions.

## 2. Config

Add one field to `Services/WhatsAppSettings.cs`:

```csharp
public string OperatorPhone { get; set; } = string.Empty;
```

The real value goes in the already-gitignored `App/appsettings.Production.json` (same file the driver-delivery work already created), alongside the existing `WhatsAppSettings.AccessToken`/`PhoneNumberId`. Not committed, matching how `AccessToken`/`PhoneNumberId` are already handled.

## 3. Notification change

In `NotificationService.SendBookingCreatedNotificationAsync`, add one more `SendAndLogAsync` call — alongside the existing operator email, not replacing it — sending to `_whatsAppSettings.OperatorPhone` via the existing `IWhatsAppSender`. Message content mirrors the existing operator email's wording style, e.g.:

```
New booking {reference}: {pickup} -> {destination} on {date} {time}.
```

This requires `NotificationService` to have access to `WhatsAppSettings` (currently it only injects `EmailSettings`) — inject `IOptions<WhatsAppSettings>` the same way `EmailSettings` is already injected.

## 4. Testing

- Update `NotificationServiceTests`'s existing `SendBookingCreatedNotificationAsync` test(s) to also assert a WhatsApp send occurred to `OperatorPhone`, using the existing `FakeWhatsAppSender` test double already used for the driver-assigned tests.
- No new test infrastructure — reuse `FakeWhatsAppSender`.

## 5. Verification

Same live path as the parent WhatsApp-delivery work: submit a real booking through the customer form, confirm a `Notifications` row with `Channel = 'WhatsApp'`, `RecipientType = 'Operator'`, `DeliveryStatus = 'Sent'`, and confirm the message arrives on the real verified test phone.

## 6. Non-goals

- No change to the existing operator email or calendar-sync behavior on booking creation — WhatsApp is additive.
- No change to any other notification event (driver-assigned, driver-accepted, booking-completed, etc.).
