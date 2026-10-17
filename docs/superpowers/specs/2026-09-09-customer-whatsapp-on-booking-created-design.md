# Customer WhatsApp Confirmation on Booking Created — Design Specification

**Project:** RideReady — also WhatsApp the customer (not just email) when they submit a booking
**Date:** 2026-09-09
**Version:** 1.0.0
**Status:** Ready for Implementation
**Related work:** `docs/superpowers/specs/2026-09-08-fare-and-trip-status-whatsapp-notifications-design.md` (same `SendAndLogAsync` pattern, same codebase)

---

## 1. Summary

`NotificationService.SendBookingCreatedNotificationAsync` currently emails the customer and emails/WhatsApps the operator when a booking is created. Diagnosing a "no WhatsApp received" report today revealed two things: (1) a token issue and a 24-hour WhatsApp messaging window issue on the operator side, both now resolved, and (2) that the customer's own confirmation currently relies solely on email — which is silently failing because SMTP credentials in `appsettings.json` are still placeholders. The customer therefore gets **no** confirmation of any kind right now.

This adds a WhatsApp send to the customer, alongside the existing (broken) email, so the customer has a working confirmation channel regardless of SMTP being configured. Fixing SMTP itself is out of scope — this only adds a second, independent channel.

## 2. Change

In `NotificationService.SendBookingCreatedNotificationAsync`, add one more `SendAndLogAsync` call for the customer, reusing the existing customer message and mirroring the exact pattern already used to extend `SendBookingCompletedNotificationAsync` and `SendDriverAcceptedNotificationAsync` in the prior WhatsApp notifications work — no new abstractions.

Placement: immediately after the existing customer email call, before the operator email/WhatsApp/calendar calls (keeps customer-facing sends grouped together).

Message (reused verbatim from the existing email):
```
Hi {CustomerName}, your RideReady reference is {BookingReference}. We'll contact you to confirm your driver.
```

Recipient: `booking.Customer.Phone`, `RecipientType = "Customer"`, `Channel = "WhatsApp"`, `EventType = "BookingCreated"` (same event type as the existing customer email row, since it's the same logical event on a new channel).

## 3. Error Handling

Unchanged from the existing pattern: goes through `SendAndLogAsync`, which never throws back to the caller. A WhatsApp failure here doesn't block booking creation, exactly like every other notification in this app.

## 4. Testing

Update the existing test `NotificationServiceTests.SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar`:
- `Assert.Single(whatsAppSender.Sent)` → `Assert.Equal(2, whatsAppSender.Sent.Count)`, with an added assertion that one of the two sends targets the customer's phone (`"0125183838"` in the existing test fixture).
- `Assert.Equal(4, notifications.Count)` → `Assert.Equal(5, notifications.Count)`.

No new test infrastructure — reuses the existing `FakeWhatsAppSender`.

## 5. Non-Goals

- No fix to the broken SMTP configuration (separate, pre-existing, unrelated issue).
- No change to Google Calendar sync.
- No change to the operator-side email/WhatsApp/calendar sends in this method.
- No change to any other notification method.
