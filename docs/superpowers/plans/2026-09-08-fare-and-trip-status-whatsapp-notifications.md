# Fare and Trip Status WhatsApp Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WhatsApp the customer when an admin sets a booking's fare, and WhatsApp both the customer and operator as a driver progresses through a trip (accepted, picked up, in transit, dropped off, completed).

**Architecture:** Two new methods on the existing `NotificationService` (`SendPriceSetNotificationAsync`, `SendTripStatusUpdateNotificationAsync`) plus WhatsApp sends added to two existing methods (`SendBookingCompletedNotificationAsync`, `SendDriverAcceptedNotificationAsync`), all using the established `SendAndLogAsync` pattern (send, log to `Notifications` table, never throw). Wired in from `AdminController.SetFare` and `DriverController.UpdateStatus`.

**Tech Stack:** ASP.NET Core MVC, EF Core (InMemory provider for tests), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-08-fare-and-trip-status-whatsapp-notifications-design.md`

---

### Task 1: `SendPriceSetNotificationAsync`

**Files:**
- Modify: `App/Services/INotificationService.cs`
- Modify: `App/Services/NotificationService.cs`
- Test: `App/Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test to `App/Tests/Services/NotificationServiceTests.cs`, inside the `NotificationServiceTests` class (e.g. right after `SendBookingCreatedNotificationAsync_WhenEmailFails_LogsFailedNotificationAndDoesNotThrow`):

```csharp
[Fact]
public async Task SendPriceSetNotificationAsync_SendsWhatsAppToCustomerWithFareAndPaymentMethod()
{
    // Arrange
    var context = GetInMemoryDbContext();
    var booking = await SeedBookingAsync(context);
    context.BookingQuotes.Add(new BookingQuote
    {
        BookingId = booking.Id,
        TotalEstimatedFare = 45.50m,
        PaymentMethod = "Bank_Transfer"
    });
    await context.SaveChangesAsync();
    var whatsAppSender = new FakeWhatsAppSender();
    var service = new NotificationService(context, new FakeEmailSender(), whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

    // Act
    await service.SendPriceSetNotificationAsync(booking.Id);

    // Assert
    Assert.Single(whatsAppSender.Sent);
    Assert.Equal("0125183838", whatsAppSender.Sent[0].To);
    Assert.Contains("RM45.50", whatsAppSender.Sent[0].Message);
    Assert.Contains("Bank Transfer", whatsAppSender.Sent[0].Message);
    var notification = await context.Notifications.SingleAsync(n => n.BookingId == booking.Id);
    Assert.Equal("Sent", notification.DeliveryStatus);
    Assert.Equal("WhatsApp", notification.Channel);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendPriceSetNotificationAsync_SendsWhatsAppToCustomerWithFareAndPaymentMethod"`
Expected: FAIL to build — `'NotificationService' does not contain a definition for 'SendPriceSetNotificationAsync'`.

- [ ] **Step 3: Add the method to the interface**

In `App/Services/INotificationService.cs`, add this line inside the interface, after `SendBookingCancelledNotificationAsync`:

```csharp
        Task SendPriceSetNotificationAsync(int bookingId);
```

- [ ] **Step 4: Implement the method**

In `App/Services/NotificationService.cs`, add this method, right after `SendDriverAssignedNotificationAsync` (before `SendDriverAcceptedNotificationAsync`):

```csharp
        public async Task SendPriceSetNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer).Include(b => b.Quote)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var paymentMethodText = booking.Quote?.PaymentMethod == "Bank_Transfer" ? "Bank Transfer" : "Pay at Pickup";
            var fare = booking.Quote?.TotalEstimatedFare ?? 0;
            var message = $"Hi {booking.Customer!.Name}, the fare for your RideReady booking {booking.BookingReference} is RM{fare:F2} ({paymentMethodText}). Thank you!";

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "PriceSet",
                null, message,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, message));
        }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendPriceSetNotificationAsync_SendsWhatsAppToCustomerWithFareAndPaymentMethod"`
Expected: `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 6: Commit**

```bash
git add App/Services/INotificationService.cs App/Services/NotificationService.cs App/Tests/Services/NotificationServiceTests.cs
git commit -m "feat: notify customer via WhatsApp when admin sets a fare"
```

---

### Task 2: `SendTripStatusUpdateNotificationAsync`

**Files:**
- Modify: `App/Services/INotificationService.cs`
- Modify: `App/Services/NotificationService.cs`
- Test: `App/Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test to `App/Tests/Services/NotificationServiceTests.cs`, right after the test added in Task 1:

```csharp
[Theory]
[InlineData("Picked_Up", "picked you up")]
[InlineData("In_Transit", "in transit")]
[InlineData("Dropped_Off", "arrived")]
public async Task SendTripStatusUpdateNotificationAsync_SendsWhatsAppToCustomerAndOperator(string status, string expectedCustomerFragment)
{
    // Arrange
    var context = GetInMemoryDbContext();
    var booking = await SeedBookingAsync(context);
    var whatsAppSender = new FakeWhatsAppSender();
    var service = new NotificationService(context, new FakeEmailSender(), whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

    // Act
    await service.SendTripStatusUpdateNotificationAsync(booking.Id, status);

    // Assert
    Assert.Equal(2, whatsAppSender.Sent.Count);
    var customerMessage = whatsAppSender.Sent.Single(s => s.To == "0125183838");
    Assert.Contains(expectedCustomerFragment, customerMessage.Message);
    var operatorMessage = whatsAppSender.Sent.Single(s => s.To == "0192462592");
    Assert.Contains(booking.BookingReference, operatorMessage.Message);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendTripStatusUpdateNotificationAsync_SendsWhatsAppToCustomerAndOperator"`
Expected: FAIL to build — `'NotificationService' does not contain a definition for 'SendTripStatusUpdateNotificationAsync'`.

- [ ] **Step 3: Add the method to the interface**

In `App/Services/INotificationService.cs`, add this line right after the `SendPriceSetNotificationAsync` line added in Task 1:

```csharp
        Task SendTripStatusUpdateNotificationAsync(int bookingId, string newStatus);
```

- [ ] **Step 4: Implement the method**

In `App/Services/NotificationService.cs`, add this method right after `SendPriceSetNotificationAsync` (the one added in Task 1):

```csharp
        public async Task SendTripStatusUpdateNotificationAsync(int bookingId, string newStatus)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var (customerMessage, operatorMessage) = newStatus switch
            {
                "Picked_Up" => (
                    $"Your RideReady driver has picked you up for booking {booking.BookingReference}. Enjoy your ride!",
                    $"Booking {booking.BookingReference}: driver has picked up the customer."),
                "In_Transit" => (
                    $"You're on your way! Booking {booking.BookingReference} is now in transit.",
                    $"Booking {booking.BookingReference} is now in transit."),
                "Dropped_Off" => (
                    $"You've arrived! Thanks for riding with RideReady (booking {booking.BookingReference}).",
                    $"Booking {booking.BookingReference}: customer dropped off."),
                _ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Not a supported trip status update")
            };

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Phone, "WhatsApp", $"TripStatus_{newStatus}",
                null, customerMessage,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, customerMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", $"TripStatus_{newStatus}",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendTripStatusUpdateNotificationAsync_SendsWhatsAppToCustomerAndOperator"`
Expected: `Passed! - Failed: 0, Passed: 3` (one per `[InlineData]` case).

- [ ] **Step 6: Commit**

```bash
git add App/Services/INotificationService.cs App/Services/NotificationService.cs App/Tests/Services/NotificationServiceTests.cs
git commit -m "feat: notify customer and operator via WhatsApp on interim trip status updates"
```

---

### Task 3: Extend `SendBookingCompletedNotificationAsync` with WhatsApp

**Files:**
- Modify: `App/Services/NotificationService.cs`
- Test: `App/Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Replace the existing test with a failing one**

In `App/Tests/Services/NotificationServiceTests.cs`, find the existing test `SendBookingCompletedNotificationAsync_SendsCustomerEmail` and replace it entirely with:

```csharp
[Fact]
public async Task SendBookingCompletedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp()
{
    // Arrange
    var context = GetInMemoryDbContext();
    var booking = await SeedBookingAsync(context);
    var emailSender = new FakeEmailSender();
    var whatsAppSender = new FakeWhatsAppSender();
    var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

    // Act
    await service.SendBookingCompletedNotificationAsync(booking.Id);

    // Assert
    Assert.Single(emailSender.Sent);
    Assert.Equal("sim@email.com", emailSender.Sent[0].To);
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendBookingCompletedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 2, Actual: 0` (no WhatsApp sends happen yet).

- [ ] **Step 3: Extend the method**

In `App/Services/NotificationService.cs`, replace the existing `SendBookingCompletedNotificationAsync` method body with:

```csharp
        public async Task SendBookingCompletedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Thanks for riding with RideReady! Your trip {booking.BookingReference} is complete.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "BookingCompleted",
                "Trip complete", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Trip complete", message));

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "BookingCompleted",
                null, message,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, message));

            var operatorMessage = $"Booking {booking.BookingReference} completed.";
            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "BookingCompleted",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendBookingCompletedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp"`
Expected: `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 5: Commit**

```bash
git add App/Services/NotificationService.cs App/Tests/Services/NotificationServiceTests.cs
git commit -m "feat: also WhatsApp customer and operator when a booking is completed"
```

---

### Task 4: Extend `SendDriverAcceptedNotificationAsync` with WhatsApp

**Files:**
- Modify: `App/Services/NotificationService.cs`
- Test: `App/Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Replace the existing test with a failing one**

In `App/Tests/Services/NotificationServiceTests.cs`, find the existing test `SendDriverAcceptedNotificationAsync_SendsCustomerEmail` and replace it entirely with:

```csharp
[Fact]
public async Task SendDriverAcceptedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp()
{
    // Arrange
    var context = GetInMemoryDbContext();
    var booking = await SeedBookingAsync(context);
    var emailSender = new FakeEmailSender();
    var whatsAppSender = new FakeWhatsAppSender();
    var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

    // Act
    await service.SendDriverAcceptedNotificationAsync(booking.Id);

    // Assert
    Assert.Single(emailSender.Sent);
    Assert.Equal("sim@email.com", emailSender.Sent[0].To);
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendDriverAcceptedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 2, Actual: 0`.

- [ ] **Step 3: Extend the method**

In `App/Services/NotificationService.cs`, replace the existing `SendDriverAcceptedNotificationAsync` method body with:

```csharp
        public async Task SendDriverAcceptedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Good news! A driver has been confirmed for your booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "DriverAccepted",
                "Driver confirmed", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Driver confirmed", message));

            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Phone, "WhatsApp", "DriverAccepted",
                null, message,
                () => _whatsAppSender.SendAsync(booking.Customer.Phone, message));

            var operatorMessage = $"Driver accepted booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Operator", null, _whatsAppSettings.OperatorPhone, "WhatsApp", "DriverAccepted",
                null, operatorMessage,
                () => _whatsAppSender.SendAsync(_whatsAppSettings.OperatorPhone, operatorMessage));
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SendDriverAcceptedNotificationAsync_SendsCustomerEmailAndWhatsAppAndOperatorWhatsApp"`
Expected: `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 5: Commit**

```bash
git add App/Services/NotificationService.cs App/Tests/Services/NotificationServiceTests.cs
git commit -m "feat: also WhatsApp customer and operator when a driver accepts a job"
```

---

### Task 5: Wire price notification into `AdminController.SetFare`

**Files:**
- Modify: `App/Controllers/AdminController.cs`
- Modify: `App/Tests/Controllers/AdminControllerTests.cs`

- [ ] **Step 1: Expose the fake WhatsApp sender from the test helpers**

In `App/Tests/Controllers/AdminControllerTests.cs`, replace the `BuildNotificationService` and `BuildController` helper methods with these (adding an optional `whatsAppSender` parameter to each, defaulting to `null` so every existing call site keeps working unchanged):

```csharp
        private static INotificationService BuildNotificationService(RideReadyDbContext context, RideReady.Tests.Services.FakeWhatsAppSender? whatsAppSender = null) =>
            new NotificationService(
                context,
                new RideReady.Tests.Services.FakeEmailSender(),
                whatsAppSender ?? new RideReady.Tests.Services.FakeWhatsAppSender(),
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

        private static AdminController BuildController(RideReadyDbContext context, bool withTempData = true, RideReady.Tests.Services.FakeWhatsAppSender? whatsAppSender = null)
        {
            var controller = new AdminController(
                new DriverAssignmentService(context),
                BuildNotificationService(context, whatsAppSender),
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

- [ ] **Step 2: Write the failing test**

Add this test right after `SetFare_WithValidFare_RedirectsAndUpdatesQuote`:

```csharp
[Fact]
public async Task SetFare_WithValidFare_SendsPriceNotificationToCustomer()
{
    // Arrange
    var (context, booking, _) = await SeedBookingAndDriverAsync();
    context.BookingQuotes.Add(new Models.BookingQuote
    {
        BookingId = booking.Id,
        TotalEstimatedFare = 0,
        PaymentMethod = "Pay_at_Pickup"
    });
    await context.SaveChangesAsync();
    var whatsAppSender = new RideReady.Tests.Services.FakeWhatsAppSender();
    var controller = BuildController(context, whatsAppSender: whatsAppSender);

    // Act
    await controller.SetFare(new SetFareViewModel { BookingId = booking.Id, Fare = 123.45m });

    // Assert
    Assert.Single(whatsAppSender.Sent);
    Assert.Equal("0125183838", whatsAppSender.Sent[0].To);
    Assert.Contains("RM123.45", whatsAppSender.Sent[0].Message);
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SetFare_WithValidFare_SendsPriceNotificationToCustomer"`
Expected: FAIL — `Assert.Single() Failure: The collection was empty`.

- [ ] **Step 4: Wire the notification call into the controller**

In `App/Controllers/AdminController.cs`, in the `SetFare` action, add the notification call right after `SetManualFareAsync` succeeds:

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFare(SetFareViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Enter a valid fare greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _bookingService.SetManualFareAsync(model.BookingId, model.Fare);
                await _notificationService.SendPriceSetNotificationAsync(model.BookingId);
                TempData["SuccessMessage"] = "Fare saved.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~SetFare_WithValidFare_SendsPriceNotificationToCustomer"`
Expected: `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 6: Run the full AdminControllerTests file to confirm no regressions**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~RideReady.Tests.Controllers.AdminControllerTests"`
Expected: all tests pass (the pre-existing `SetFare_WithValidFare_RedirectsAndUpdatesQuote` and `SetFare_WithNonexistentBooking_...` tests still pass unchanged, since `whatsAppSender` defaults to `null` there).

- [ ] **Step 7: Commit**

```bash
git add App/Controllers/AdminController.cs App/Tests/Controllers/AdminControllerTests.cs
git commit -m "feat: send the customer a price notification when admin sets the fare"
```

---

### Task 6: Wire trip status notifications into `DriverController`

**Files:**
- Modify: `App/Controllers/DriverController.cs`
- Modify: `App/Tests/Controllers/DriverControllerTests.cs`

- [ ] **Step 1: Expose the fake WhatsApp sender from the test helpers**

In `App/Tests/Controllers/DriverControllerTests.cs`, replace the `BuildNotificationService` and `WithAuthenticatedDriver` helper methods with these (adding an optional `whatsAppSender` parameter, defaulting to `null`):

```csharp
        private static INotificationService BuildNotificationService(RideReadyDbContext context, RideReady.Tests.Services.FakeWhatsAppSender? whatsAppSender = null) =>
            new NotificationService(
                context,
                new RideReady.Tests.Services.FakeEmailSender(),
                whatsAppSender ?? new RideReady.Tests.Services.FakeWhatsAppSender(),
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

        private static DriverController WithAuthenticatedDriver(RideReadyDbContext context, IDriverPortalService service, int driverId, RideReady.Tests.Services.FakeWhatsAppSender? whatsAppSender = null)
        {
            var controller = new DriverController(service, BuildNotificationService(context, whatsAppSender))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, driverId.ToString()) },
                            "TestAuth"))
                    }
                },
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new DefaultHttpContext(),
                    new NullTempDataProvider())
            };
            return controller;
        }
```

- [ ] **Step 2: Write the failing tests**

Add these three tests right after `UpdateStatus_WithInvalidStatus_RedirectsWithErrorMessageInsteadOfThrowing`:

```csharp
[Theory]
[InlineData("Picked_Up")]
[InlineData("In_Transit")]
[InlineData("Dropped_Off")]
public async Task UpdateStatus_WithInterimStatus_SendsTripStatusWhatsAppToCustomerAndOperator(string newStatus)
{
    // Arrange
    var (context, driver, booking, assignment) = await SeedAssignedBookingAsync();
    assignment.AssignmentStatus = "Accepted";
    await context.SaveChangesAsync();
    var service = new DriverPortalService(context);
    var whatsAppSender = new RideReady.Tests.Services.FakeWhatsAppSender();
    var controller = WithAuthenticatedDriver(context, service, driver.Id, whatsAppSender);

    // Act
    await controller.UpdateStatus(booking.Id, newStatus);

    // Assert
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
}

[Fact]
public async Task UpdateStatus_WithCompletedStatus_SendsCompletedNotifications()
{
    // Arrange
    var (context, driver, booking, assignment) = await SeedAssignedBookingAsync();
    assignment.AssignmentStatus = "Accepted";
    await context.SaveChangesAsync();
    var service = new DriverPortalService(context);
    var whatsAppSender = new RideReady.Tests.Services.FakeWhatsAppSender();
    var controller = WithAuthenticatedDriver(context, service, driver.Id, whatsAppSender);

    // Act
    await controller.UpdateStatus(booking.Id, "Completed");

    // Assert
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
}

[Fact]
public async Task Accept_WithValidAssignment_SendsWhatsAppToCustomerAndOperator()
{
    // Arrange
    var (context, driver, booking, assignment) = await SeedAssignedBookingAsync();
    var service = new DriverPortalService(context);
    var whatsAppSender = new RideReady.Tests.Services.FakeWhatsAppSender();
    var controller = WithAuthenticatedDriver(context, service, driver.Id, whatsAppSender);

    // Act
    await controller.Accept(assignment.Id);

    // Assert
    Assert.Equal(2, whatsAppSender.Sent.Count);
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
    Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~UpdateStatus_WithInterimStatus_SendsTripStatusWhatsAppToCustomerAndOperator|FullyQualifiedName~UpdateStatus_WithCompletedStatus_SendsCompletedNotifications"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 2, Actual: 0` for all four cases (interim statuses trigger nothing yet, and `UpdateStatus` never calls a completed notification).

`Accept_WithValidAssignment_SendsWhatsAppToCustomerAndOperator` should already PASS at this point, since Task 4 already extended `SendDriverAcceptedNotificationAsync` (which `Accept` already calls) — confirm this with:

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~Accept_WithValidAssignment_SendsWhatsAppToCustomerAndOperator"`
Expected: `Passed! - Failed: 0, Passed: 1` (no controller code change needed for this one — it's covered by Task 4's service-level change).

- [ ] **Step 4: Wire the notification calls into `UpdateStatus`**

In `App/Controllers/DriverController.cs`, replace the `UpdateStatus` action with:

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            try
            {
                await _driverPortalService.UpdateTripStatusAsync(bookingId, GetCurrentDriverId(), newStatus);

                if (newStatus == "Completed")
                {
                    await _notificationService.SendBookingCompletedNotificationAsync(bookingId);
                }
                else
                {
                    await _notificationService.SendTripStatusUpdateNotificationAsync(bookingId, newStatus);
                }

                TempData["SuccessMessage"] = "Status updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test App/RideReady.csproj --filter "FullyQualifiedName~RideReady.Tests.Controllers.DriverControllerTests"`
Expected: all tests pass, including the three new ones and the pre-existing `UpdateStatus_WithInvalidStatus_RedirectsWithErrorMessageInsteadOfThrowing` (unaffected, since it fails validation before reaching the notification calls).

- [ ] **Step 6: Commit**

```bash
git add App/Controllers/DriverController.cs App/Tests/Controllers/DriverControllerTests.cs
git commit -m "feat: notify customer and operator via WhatsApp as a driver updates trip status"
```

---

### Task 7: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test App/RideReady.csproj`
Expected: every test in the project passes (0 failures) — this includes all pre-existing tests (`AdminAuthControllerTests`, `BookingControllerTests`, `DriverAuthControllerTests`, the `Jobs` tests, `BookingServiceTests`, `DriverAssignmentServiceTests`, `DriverAvailabilityServiceTests`, `DriverPortalServiceTests`, `GoogleMapsLocationService*Tests`, `PasswordHasherTests`, `WhatsAppCloudApiSenderTests`) plus everything added in Tasks 1-6. If anything fails, stop and fix it before continuing.

- [ ] **Step 2: Build in Release mode (matches CI)**

Run: `dotnet build App/RideReady.csproj --configuration Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` (or pre-existing warning count unchanged — don't introduce new warnings).

No commit for this task — it's verification only, and Task 6 already committed everything Step 1 exercises.
