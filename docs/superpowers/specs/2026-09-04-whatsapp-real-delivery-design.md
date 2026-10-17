# WhatsApp Real Delivery (Dev/Test Tier) — Design Specification

**Project:** RideReady — Get real WhatsApp Business Cloud API delivery working for driver-assignment notices
**Date:** 2026-09-04
**Version:** 1.0.0
**Status:** Ready for Implementation

---

## 1. Summary

`WhatsAppCloudApiSender` already implements the Meta WhatsApp Business Cloud API call correctly (Bearer-token auth, `type: "text"` payload against `{ApiUrl}/{PhoneNumberId}/messages`), but `appsettings.json` only ever held placeholder credentials (`YOUR_ACCESS_TOKEN`, `YOUR_PHONE_NUMBER_ID`), so no notification has ever actually been delivered — every row in the `Notifications` table shows `DeliveryStatus = Failed`.

This is a config-and-verification task, not a feature build: wire up real Meta test-tier credentials, fix one diagnosability gap in the sender, and verify a real message actually reaches a real phone through the existing `AssignDriver` → `SendDriverAssignedNotificationAsync` → `WhatsAppCloudApiSender` path — no new code paths.

**Explicitly out of scope:**
- WhatsApp message **templates** (required by Meta for business-initiated messages outside a 24-hour customer-service window). The test tier's verified-recipient flow opens that window, so free-form `text` messages work for this tier. Template support is a real requirement for a production WhatsApp Business number, but is deferred until that tier is actually pursued.
- Any change to `docker-compose.yml`'s `ASPNETCORE_ENVIRONMENT: Production` setting — that file is shared with the real droplet deployment; changing it is out of scope here.
- New notification event types or message wording changes — this is purely about making the existing "driver assigned" WhatsApp notice actually deliver.

---

## 2. Credential storage

Real credentials go into a new `App/appsettings.Production.json`, matching the existing `WhatsAppSettings` shape (`ApiUrl`, `AccessToken`, `PhoneNumberId`):

```json
{
  "WhatsAppSettings": {
    "AccessToken": "<real token>",
    "PhoneNumberId": "<real phone number ID>"
  }
}
```

**Why `Production`, not `Development`:** `docker-compose.yml` (used by `run.bat` for local runs) sets `ASPNETCORE_ENVIRONMENT: Production`. ASP.NET Core only auto-loads `appsettings.{ASPNETCORE_ENVIRONMENT}.json` over the base `appsettings.json` — an `appsettings.Development.json` file would never be read by the actual local dev stack. `appsettings.Production.json` is the file that matches the environment this app really runs under locally.

**No `.gitignore` change needed:** `App/.gitignore` already has `appsettings.*.json`, so `appsettings.Production.json` is excluded from git automatically — confirmed by inspecting the existing file.

`ApiUrl` (`https://graph.facebook.com/v18.0`) is not sensitive and stays in the committed base `appsettings.json` — only `AccessToken` and `PhoneNumberId` are overridden.

---

## 3. Error diagnosability fix

`WhatsAppCloudApiSender.SendAsync` currently calls `response.EnsureSuccessStatusCode()`, which throws `HttpRequestException` with only a generic message (e.g. `"Response status code does not indicate success: 401 (Unauthorized)."`) — it discards Meta's actual JSON error body (e.g. `{"error":{"message":"Invalid OAuth access token","type":"OAuthException","code":190}}`), which is what's actually needed to tell "bad token" apart from "bad phone number ID" apart from "recipient not a verified tester."

**Fix:** read the response body before checking success; if the call failed, throw an exception whose message includes both the HTTP status and the response body. This requires no changes anywhere else — `NotificationService.SendAndLogAsync`'s existing `catch (Exception ex) { ... ErrorMessage = ex.Message; }` already persists whatever message reaches it into the `Notifications` table, so the richer error automatically becomes visible there.

---

## 4. Verification plan

1. Create `App/appsettings.Production.json` with the real `AccessToken`/`PhoneNumberId` (provided directly, not committed).
2. In the database, set one test driver's `Phone` to the verified test-recipient number registered in the Meta dashboard (`+1 555-672-9886` per the account already set up).
3. Rebuild and restart the local Docker stack (`run.bat`) so the new config is picked up.
4. Through the normal admin flow — no new test-only code — assign that driver to a booking (`Admin/Index` → `AssignDriver`), which triggers the existing `SendDriverAssignedNotificationAsync` → `WhatsAppCloudApiSender.SendAsync` path exactly as it runs today.
5. Confirm success two ways: the corresponding `Notifications` row shows `DeliveryStatus = "Sent"` (not `"Failed"`), and the message actually arrives on the real WhatsApp app for that test number.
6. If it fails, the Section 3 fix means the `ErrorMessage` column now carries Meta's specific reason to act on, rather than a bare HTTP status code.

---

## 5. Non-goals / future work

- Message template support for production-tier WhatsApp Business numbers (deferred — see §1).
- Any other notification channel or event type changes.
- Changing `ASPNETCORE_ENVIRONMENT` for local runs (deferred — user chose to keep `docker-compose.yml` as-is).
