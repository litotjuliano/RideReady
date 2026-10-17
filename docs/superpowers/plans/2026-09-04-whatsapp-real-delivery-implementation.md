# WhatsApp Real Delivery (Dev/Test Tier) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Get real WhatsApp Business Cloud API delivery working end-to-end through the existing `AssignDriver` → `SendDriverAssignedNotificationAsync` → `WhatsAppCloudApiSender` path, with no new code paths.

**Architecture:** `WhatsAppCloudApiSender` already implements the Meta Cloud API call correctly — it just needs (1) real credentials in a gitignored config file, and (2) a small fix so a failed send surfaces Meta's actual error body instead of a bare HTTP status code, since a first-time real-credential wire-up is where a mistake is most likely and most needs to be diagnosable.

**Tech Stack:** ASP.NET Core 8, `HttpClient`, Meta WhatsApp Business Cloud API, xUnit

**Spec:** `docs/superpowers/specs/2026-09-04-whatsapp-real-delivery-design.md`

All commands below assume the working directory is `App/` (where `RideReady.csproj` lives).

---

## Task 1: Surface Meta's actual error body on a failed send

**Files:**
- Modify: `Services/WhatsAppCloudApiSender.cs`
- Create: `Tests/Services/WhatsAppCloudApiSenderTests.cs`

**Interfaces:**
- No signature change — `IWhatsAppSender.SendAsync(string toPhone, string message)` is unchanged; only what it throws on failure changes.

This codebase already has a reusable `FakeHttpMessageHandler` (public class, in `Tests/Services/GoogleMapsLocationServiceTests.cs`, namespace `RideReady.Tests.Services`) that returns a canned response body/status code and counts calls — reuse it rather than defining a new one.

- [ ] **Step 1: Write failing tests**

```csharp
// Tests/Services/WhatsAppCloudApiSenderTests.cs
using System.Net;
using Microsoft.Extensions.Options;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
{
    public class WhatsAppCloudApiSenderTests
    {
        private static (WhatsAppCloudApiSender Sender, FakeHttpMessageHandler Handler) CreateSender(
            string responseBody, HttpStatusCode statusCode)
        {
            var handler = new FakeHttpMessageHandler(responseBody, statusCode);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new WhatsAppSettings
            {
                ApiUrl = "https://graph.facebook.com/v18.0",
                AccessToken = "test-token",
                PhoneNumberId = "1234567890"
            });
            var sender = new WhatsAppCloudApiSender(httpClient, settings);
            return (sender, handler);
        }

        [Fact]
        public async Task SendAsync_WithSuccessResponse_CompletesWithoutThrowing()
        {
            // Arrange
            var (sender, handler) = CreateSender("{\"messages\":[{\"id\":\"wamid.abc\"}]}", HttpStatusCode.OK);

            // Act
            await sender.SendAsync("0123456789", "Test message");

            // Assert
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_WithFailureResponse_ThrowsExceptionContainingMetaErrorBody()
        {
            // Arrange
            const string errorBody = "{\"error\":{\"message\":\"Invalid OAuth access token\",\"type\":\"OAuthException\",\"code\":190}}";
            var (sender, _) = CreateSender(errorBody, HttpStatusCode.Unauthorized);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync("0123456789", "Test message"));
            Assert.Contains("Invalid OAuth access token", ex.Message);
            Assert.Contains("401", ex.Message);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the failure test fails**

Run: `dotnet test --filter FullyQualifiedName~WhatsAppCloudApiSenderTests`
Expected: `SendAsync_WithSuccessResponse_CompletesWithoutThrowing` PASSES (current code already succeeds silently on a 200), `SendAsync_WithFailureResponse_ThrowsExceptionContainingMetaErrorBody` FAILS — `response.EnsureSuccessStatusCode()` throws `HttpRequestException` with a message like `"Response status code does not indicate success: 401 (Unauthorized)."`, which does not contain `"Invalid OAuth access token"`

- [ ] **Step 3: Fix `SendAsync` to capture the response body**

In `Services/WhatsAppCloudApiSender.cs`, replace:

```csharp
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
```

with:

```csharp
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"WhatsApp API request failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~WhatsAppCloudApiSenderTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS (all tests, no regressions)

- [ ] **Step 6: Commit**

```bash
git add Services/WhatsAppCloudApiSender.cs Tests/Services/WhatsAppCloudApiSenderTests.cs
git commit -m "fix: surface WhatsApp API error body instead of bare HTTP status"
```

---

## Task 2: Real credentials for the local dev stack

**Files:**
- Create: `App/appsettings.Production.json` (gitignored — never committed)

**Interfaces:** None — pure configuration, overrides `WhatsAppSettings` (`AccessToken`, `PhoneNumberId`) from the placeholder values in the committed `appsettings.json`.

- [ ] **Step 1: Confirm the file is actually gitignored before creating it**

Run: `git check-ignore -v App/appsettings.Production.json`
Expected: prints a match against the `appsettings.*.json` line in `App/.gitignore` — confirms the file will not be trackable before it exists with real secrets in it

- [ ] **Step 2: Create `App/appsettings.Production.json`**

```json
{
  "WhatsAppSettings": {
    "AccessToken": "<real Meta test-app access token>",
    "PhoneNumberId": "<real Meta test-app phone number ID>"
  }
}
```

The two placeholder values above are supplied directly by whoever runs this step (they were already shared out-of-band during the brainstorming session that produced this plan) — this plan document intentionally does not carry the real secret values.

`ApiUrl` is not overridden here — the base `appsettings.json`'s `"https://graph.facebook.com/v18.0"` is correct and not sensitive.

- [ ] **Step 3: Verify `git status` shows the new file as untracked-and-ignored, not stageable**

Run: `git status --porcelain --ignored | grep appsettings.Production`
Expected: the file appears prefixed with `!!` (git's marker for an ignored file), not as a plain untracked `??` entry

- [ ] **Step 4: No commit for this task**

This file must never be committed — there is nothing to `git add` here. Proceed to Task 3.

---

## Task 3: Rebuild, assign a driver, verify real delivery

**Files:** None (verification task)

- [ ] **Step 1: Rebuild and restart the local Docker stack**

Run `run.bat` from the repo root (or, if already running, `docker compose down` then re-run it) so the container picks up both the Task 1 code fix and the Task 2 config file. Wait for `/health` to report healthy.

- [ ] **Step 2: Point a test driver's phone number at the verified Meta test recipient**

In the running Postgres container, update one active driver's `Phone` column to the verified test-recipient number registered in the Meta dashboard (`+1 555-672-9886` per the account set up during brainstorming), e.g.:

```bash
docker exec rideready-db-1 psql -U rideuser -d rideready -c "UPDATE \"Drivers\" SET \"Phone\" = '+15556729886' WHERE \"Id\" = <driver id>;"
```

Pick a driver ID that is not already involved in an active assignment conflicting with the booking you'll use in Step 3 (check via the `Admin/Index` dashboard or a `SELECT` against `Bookings`/`DriverAssignments` first).

- [ ] **Step 3: Assign that driver to a booking through the normal admin flow**

Log into `/AdminAuth/Login`, go to `Admin/Index`, and use the existing "Assign" dropdown/button on any open booking to assign the driver from Step 2. This exercises the real, unmodified `AdminController.AssignDriver` → `DriverAssignmentService.AssignDriverAsync` → `NotificationService.SendDriverAssignedNotificationAsync` → `WhatsAppCloudApiSender.SendAsync` path — no test-only code.

- [ ] **Step 4: Confirm delivery in the database**

```bash
docker exec rideready-db-1 psql -U rideuser -d rideready -c "SELECT \"Id\", \"Channel\", \"DeliveryStatus\", \"ErrorMessage\", \"SentAt\" FROM \"Notifications\" WHERE \"Channel\" = 'WhatsApp' ORDER BY \"Id\" DESC LIMIT 1;"
```

Expected: `DeliveryStatus = 'Sent'`, `SentAt` populated, `ErrorMessage` null.

If it instead shows `DeliveryStatus = 'Failed'`, the `ErrorMessage` column (thanks to Task 1's fix) now contains Meta's actual error body — read it to determine the fix (e.g. "Invalid OAuth access token" → token is wrong/expired; "recipient phone number not in allowed list" → the number in Step 2 doesn't exactly match the verified tester format Meta expects, e.g. missing/extra `+` or spacing — check the exact format shown in the Meta dashboard's tester list).

- [ ] **Step 5: Confirm delivery on the real phone**

Check the WhatsApp app on the phone number used in Step 2 for the actual message: `"New job <reference>: pickup <location> -> <destination> on <date> <time>. Log in to the Driver Portal to accept or reject."`

- [ ] **Step 6: Note completion**

No commit needed for this task — it's verification only. If Step 4 or Step 5 reveals a real bug in the `SendAsync` request construction (as opposed to a credential/config mistake), fix it as a new commit, re-run the full test suite, and repeat this task's verification before considering the plan complete.
