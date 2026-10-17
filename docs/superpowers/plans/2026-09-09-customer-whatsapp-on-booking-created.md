# Customer WhatsApp on Booking Created Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WhatsApp the customer (not just email) when they submit a booking, so they have a working confirmation channel even while SMTP is misconfigured.

**Architecture:** One additional `SendAndLogAsync` call in the existing `SendBookingCreatedNotificationAsync` method, reusing the established pattern already used throughout `NotificationService.cs`.

**Tech Stack:** ASP.NET Core, EF Core (InMemory provider for tests), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-09-customer-whatsapp-on-booking-created-design.md`

---

### Task 1: Add customer WhatsApp send to `SendBookingCreatedNotificationAsync`

**Files:**
- Modify: `App/Services/NotificationService.cs`
- Modify: `App/Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Update the existing test to expect the new send**

In `App/Tests/Services/NotificationServiceTests.cs`, find the test `SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar` and replace it entirely with:

```csharp
[Fact]
public async Task SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar()
{
    // Arrange
    var context = GetInMemoryDbContext();
    var booking = await SeedBookingAsync(context);
    var emailSender = new FakeEmailSender();
    var whatsAppSender = new FakeWhatsAppSender();
    var calendarSync = new FakeCalendarSyncService();
    var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings(), WhatsAppOptions());

    // Act
    await service.SendBookingCreatedNotificationAsync(booking.Id);

    // Assert
    Assert.Equal(2, emailSender.Sent.Count);
    Assert.Contains(emailSender.Sent, s => s.To == "sim@email.com");
    Assert.Contains(emailSender.Sent, s => s.To == "operator@rideready.my");
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
    Assert.Equal(1, calendarSync.CallCount);
    var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
    Assert.Equal(5, notifications.Count);
    Assert.All(notifications, n => Assert.Equal("Sent", n.DeliveryStatus));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 2, Actual: 1` (only the operator WhatsApp send happens today).

- [ ] **Step 3: Add the customer WhatsApp send**

In `App/Services/NotificationService.cs`, in `SendBookingCreatedNotificationAsync`, insert this block right after the existing customer email `SendAndLogAsync` call (after line 42, before the `var operatorMessage = ...` line):

```csharp
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "BookingCreated",
                null, customerMessage,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, customerMessage));

```

The full method should read:

```csharp
        public async Task SendBookingCreatedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var customerMessage = $"Hi {booking.Customer!.Name}, your RideReady reference is {booking.BookingReference}. We'll contact you to confirm your driver.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Email, "Email", "BookingCreated",
                "Your RideReady reservation", customerMessage,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideReady reservation", customerMessage));

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "BookingCreated",
                null, customerMessage,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, customerMessage));

            var operatorMessage = $"New booking {booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}.";
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "BookingCreated",
                "New booking received", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "BookingCreated",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Calendar", "BookingCreated",
                null, "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar"`
Expected: `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 5: Run the full NotificationServiceTests file to confirm no regressions**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~RideReady.Tests.Services.NotificationServiceTests"`
Expected: all tests pass (no other test in this file touches `SendBookingCreatedNotificationAsync`, so nothing else should be affected).

- [ ] **Step 6: Commit**

```bash
git add App/Services/NotificationService.cs App/Tests/Services/NotificationServiceTests.cs
git commit -m "feat: also WhatsApp the customer when a booking is created"
```

---

### Task 2: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test App/RideReady.csproj`
Expected: `Failed: 7, Passed: 116, Total: 123` — identical to the current baseline on `main`, since Task 1 modified an existing test rather than adding a new one. The 7 failures are the same pre-existing, unrelated ones in `BookingServiceTests.cs`/`BookingControllerTests.cs` (hardcoded past date) — confirm no new failures were introduced.

No commit for this task — verification only.
