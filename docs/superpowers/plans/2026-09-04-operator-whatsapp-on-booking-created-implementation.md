# Operator WhatsApp Notice on Booking Created Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send the operator a WhatsApp message (in addition to the existing email) whenever a customer submits a new booking.

**Architecture:** Add an `OperatorPhone` field to the existing `WhatsAppSettings` options class, inject it into `NotificationService` the same way `EmailSettings` already is, and add one more `SendAndLogAsync` call in `SendBookingCreatedNotificationAsync` — mirroring the exact pattern `SendDriverAssignedNotificationAsync` already uses to WhatsApp a driver.

**Tech Stack:** ASP.NET Core 8, `IOptions<T>`, xUnit

**Spec:** `docs/superpowers/specs/2026-09-04-operator-whatsapp-on-booking-created-design.md`

All commands below assume the working directory is `App/` (where `RideReady.csproj` lives).

---

## Task 1: `OperatorPhone` setting + operator WhatsApp send on booking creation

**Files:**
- Modify: `Services/WhatsAppSettings.cs`
- Modify: `Services/NotificationService.cs`
- Modify: `Tests/Services/NotificationServiceTests.cs`
- Modify: `Tests/Controllers/AdminControllerTests.cs`

**Interfaces:**
- `NotificationService`'s constructor gains a 6th parameter, `IOptions<WhatsAppSettings> whatsAppSettings` — every existing direct construction of `NotificationService` in tests must be updated.

- [ ] **Step 1: Add `OperatorPhone` to `WhatsAppSettings`**

In `Services/WhatsAppSettings.cs`, change:

```csharp
namespace RideReady.Services
{
    public class WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
    }
}
```

to:

```csharp
namespace RideReady.Services
{
    public class WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string OperatorPhone { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Add a `WhatsAppOptions()` test helper and write the failing test**

In `Tests/Services/NotificationServiceTests.cs`, add this helper right after the existing `Settings()` method:

```csharp
        private static IOptions<WhatsAppSettings> WhatsAppOptions() => Options.Create(new WhatsAppSettings
        {
            ApiUrl = "https://graph.facebook.com/v18.0",
            AccessToken = "test-token",
            PhoneNumberId = "1234567890",
            OperatorPhone = "0192462592"
        });
```

Then replace the existing `SendBookingCreatedNotificationAsync_SendsEmailToCustomerAndOperatorAndSyncsCalendar` test:

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
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings());

            // Act
            await service.SendBookingCreatedNotificationAsync(booking.Id);

            // Assert
            Assert.Equal(2, emailSender.Sent.Count);
            Assert.Contains(emailSender.Sent, s => s.To == "sim@email.com");
            Assert.Contains(emailSender.Sent, s => s.To == "operator@rideready.my");
            Assert.Equal(1, calendarSync.CallCount);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(3, notifications.Count);
            Assert.All(notifications, n => Assert.Equal("Sent", n.DeliveryStatus));
        }
```

with:

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
            Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0192462592", whatsAppSender.Sent[0].To);
            Assert.Equal(1, calendarSync.CallCount);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(4, notifications.Count);
            Assert.All(notifications, n => Assert.Equal("Sent", n.DeliveryStatus));
        }
```

- [ ] **Step 3: Add `WhatsAppOptions()` to the remaining 7 `new NotificationService(...)` call sites**

In `Tests/Services/NotificationServiceTests.cs`, each of the following lines currently ends with `Settings());` — change each to end with `Settings(), WhatsAppOptions());` instead. All 7 are otherwise identical to their current form:

- `SendBookingCreatedNotificationAsync_WhenEmailFails_LogsFailedNotificationAndDoesNotThrow`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings(), WhatsAppOptions());
  ```
- `SendDriverAssignedNotificationAsync_SendsWhatsAppToDriverAndEmailToOperator`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```
- `SendBookingCancelledNotificationAsync_WithDriverAssigned_SendsCustomerEmailAndDriverWhatsApp`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```
- `SendBookingCancelledNotificationAsync_WithNoDriverAssigned_SendsOnlyCustomerEmail`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```
- `SendBookingCancelledNotificationAsync_WhenOnlyAssignmentWasRejected_SendsOnlyCustomerEmail`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```
- `SendBookingCompletedNotificationAsync_SendsCustomerEmail`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```
- `SendDriverAcceptedNotificationAsync_SendsCustomerEmail`:
  ```csharp
  var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());
  ```

- [ ] **Step 4: Update `AdminControllerTests.cs`'s `BuildNotificationService` helper**

In `Tests/Controllers/AdminControllerTests.cs`, change:

```csharp
        private static INotificationService BuildNotificationService(RideReadyDbContext context) =>
            new NotificationService(
                context,
                new RideReady.Tests.Services.FakeEmailSender(),
                new RideReady.Tests.Services.FakeWhatsAppSender(),
                new RideReady.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    SenderEmail = "noreply@rideready.my",
                    SenderName = "RideReady",
                    OperatorEmail = "operator@rideready.my"
                }));
```

to:

```csharp
        private static INotificationService BuildNotificationService(RideReadyDbContext context) =>
            new NotificationService(
                context,
                new RideReady.Tests.Services.FakeEmailSender(),
                new RideReady.Tests.Services.FakeWhatsAppSender(),
                new RideReady.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    SenderEmail = "noreply@rideready.my",
                    SenderName = "RideReady",
                    OperatorEmail = "operator@rideready.my"
                }),
                Microsoft.Extensions.Options.Options.Create(new WhatsAppSettings
                {
                    ApiUrl = "https://graph.facebook.com/v18.0",
                    AccessToken = "test-token",
                    PhoneNumberId = "1234567890",
                    OperatorPhone = "0192462592"
                }));
```

- [ ] **Step 5: Run tests to verify they fail**

Run: `dotnet test`
Expected: FAIL to compile — `NotificationService`'s constructor doesn't yet accept a 6th argument

- [ ] **Step 6: Update `NotificationService`'s constructor and add the operator WhatsApp send**

In `Services/NotificationService.cs`, change:

```csharp
        private readonly RideReadyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;

        public NotificationService(
            RideReadyDbContext context,
            IEmailSender emailSender,
            IWhatsAppSender whatsAppSender,
            ICalendarSyncService calendarSyncService,
            IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
            _calendarSyncService = calendarSyncService;
            _emailSettings = emailSettings.Value;
        }
```

to:

```csharp
        private readonly RideReadyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;
        private readonly WhatsAppSettings _whatsAppSettings;

        public NotificationService(
            RideReadyDbContext context,
            IEmailSender emailSender,
            IWhatsAppSender whatsAppSender,
            ICalendarSyncService calendarSyncService,
            IOptions<EmailSettings> emailSettings,
            IOptions<WhatsAppSettings> whatsAppSettings)
        {
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
            _calendarSyncService = calendarSyncService;
            _emailSettings = emailSettings.Value;
            _whatsAppSettings = whatsAppSettings.Value;
        }
```

Then, in the same file, change `SendBookingCreatedNotificationAsync`:

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

            var operatorMessage = $"New booking {booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}.";
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "BookingCreated",
                "New booking received", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Calendar", "BookingCreated",
                null, "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }
```

to:

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

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS (all tests, same total count as before Step 2 plus no new `[Fact]` methods — Step 2 modified an existing test in place, it did not add one)

- [ ] **Step 8: Commit**

```bash
git add Services/WhatsAppSettings.cs Services/NotificationService.cs Tests/Services/NotificationServiceTests.cs Tests/Controllers/AdminControllerTests.cs
git commit -m "feat: send operator a WhatsApp notice when a customer submits a booking"
```

---

## Task 2: Real `OperatorPhone` value

**Files:**
- Modify: `App/appsettings.Production.json` (already exists and is gitignored, from the earlier WhatsApp-delivery work — this task only adds one field to it)

**Interfaces:** None — pure configuration.

- [ ] **Step 1: Add `OperatorPhone` to the existing `WhatsAppSettings` section**

`App/appsettings.Production.json` already has `AccessToken` and `PhoneNumberId` from earlier work. Add `OperatorPhone` alongside them:

```json
{
  "WhatsAppSettings": {
    "AccessToken": "<already present — do not change>",
    "PhoneNumberId": "<already present — do not change>",
    "OperatorPhone": "0192462592"
  }
}
```

(The real `AccessToken`/`PhoneNumberId` values already in the file are left untouched — only `OperatorPhone` is being added.)

- [ ] **Step 2: No commit for this task**

This file is gitignored and must never be committed. Proceed to Task 3.

---

## Task 3: Rebuild and verify live

**Files:** None (verification task)

- [ ] **Step 1: Run the full test suite one more time**

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 2: Rebuild and restart the local Docker stack**

Run `run.bat` from the repo root so the container picks up both the Task 1 code change and the Task 2 config value. Wait for `/health` to report healthy (check `docker ps` / `curl http://localhost:5000/health` directly if the script's own health-wait loop reports an error — it has a known flaky polling issue unrelated to app health).

- [ ] **Step 3: Submit a real booking through the customer form**

Go to `/Booking/Create`, fill in and submit a new booking with any valid pickup/destination/date/time. This exercises the real, unmodified `BookingController` → `BookingService.CreateBookingAsync` → `NotificationService.SendBookingCreatedNotificationAsync` path — no test-only code.

- [ ] **Step 4: Confirm delivery in the database**

```bash
docker exec rideready-db-1 psql -U rideuser -d rideready -c "SELECT \"Id\", \"RecipientType\", \"Channel\", \"DeliveryStatus\", \"ErrorMessage\", \"SentAt\" FROM \"Notifications\" WHERE \"Channel\" = 'WhatsApp' AND \"RecipientType\" = 'Operator' ORDER BY \"Id\" DESC LIMIT 1;"
```

Expected: `DeliveryStatus = 'Sent'`, `SentAt` populated, `ErrorMessage` null.

If it instead shows `DeliveryStatus = 'Failed'`, the `ErrorMessage` column (per the earlier WhatsApp-delivery diagnosability fix) contains Meta's actual error — likely either an expired temporary access token (get a fresh one from the Meta dashboard's API Setup page, same as before) or `OperatorPhone` not matching the exact verified-recipient format registered in the Meta dashboard.

- [ ] **Step 5: Confirm delivery on the real phone**

Check the WhatsApp app on the operator's phone number used in Task 2 for the actual message: `"New booking <reference>: <pickup> -> <destination> on <date> <time>."`

- [ ] **Step 6: Note completion**

No commit needed for this task — it's verification only. If Step 4 or Step 5 reveals a real bug in the request/message construction (as opposed to a credential/config mistake), fix it as a new commit, re-run the full test suite, and repeat this task's verification before considering the plan complete.
