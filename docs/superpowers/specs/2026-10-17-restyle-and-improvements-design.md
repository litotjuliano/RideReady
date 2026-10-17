# RideReady restyle and usability improvements: design

Date: 2026-10-17
Branch: `feature/design-restyle`
Status: design approved by the product owner; implementation plan to follow.

## Goal

Restyle the booking, admin and driver pages to the RideReady design pack (`rideready-design/`), and add a small
set of usability improvements chosen by the product owner, one page at a time (Booking, then Admin, then Driver).

## Decisions

| Topic | Decision |
|---|---|
| Scope | Look (design pack) + small wording tweaks + form behaviour improvements + back-office usability. |
| Delivery | Per page, in this order: Booking, Admin, Driver. Each page gets two separate commits: (1) look-only restyle with no controller, model or validation change; (2) that page's improvements, with tests. The owner reviews each page before the next one starts. |
| Release | Nothing reaches production until the owner approves everything and the branch is merged to `main` (only `main` deploys). |
| Theme | Light only for now. Dark mode is added last, after all pages are approved. |
| Background | Plain `--surface-100`; the road pattern is removed (done). |
| Progress steps | Plain connecting lines, as in the design (not arrows). |
| Driver privacy | Drivers never see the price, the payment method or the customer's email. |
| Database | No database changes anywhere in this design. |

## 1. Booking page

Look (commit 1) is built: header with logo plate, hero with steps and the Let's go sticker, booking card, picture
cards for Car/Van/Bus, confirmation page. Improvements (commit 2):

1. **Plain-words error messages.** Replace framework texts in `BookingRequestViewModel` with short, friendly ones.
   Examples: name "Please enter your full name."; phone "Enter a Malaysian phone number, like 012-345 6789.";
   email "Enter a full email address, like you@example.com."; passengers "Choose 1 to 8 passengers."; bags "Choose
   0 to 10 bags."; vehicle "Choose a vehicle type."; and the address, date and time messages in the same voice.
   Client-side messages follow automatically because unobtrusive validation reads the same attributes.
   Test: no validation message contains "The field", "must be a string" or "minimum length".
2. **Sensible defaults.** `BookingController.Create` (GET) pre-selects Car and sets Passengers to 1. POST handling
   and validation are unchanged.
3. **Live trip summary** above the Submit button: From to To, pickup date and time, approximate drop-off,
   passengers, bags and vehicle. Updates as the customer types; empty rows are hidden. View and JavaScript only.
4. **Remember on this device only.** After a valid submit the browser stores name, phone and email in
   `localStorage`. On the next visit a bar offers "Welcome back, <name>. Use your saved details?" with **Use** and
   **Forget me**. Nothing is filled silently, nothing is looked up on the server, and storage errors are caught.
5. **Confirmation page.** A "What happens next" list (we review, we assign a driver, we contact you) and a
   **Copy reference** button.
6. **Vehicle size hints** under Car, Van and Bus ("up to N passengers"). The capacities must be supplied by the
   owner; they are kept in one small block in the view.

## 2. Admin pages

Look (commit 1): banner with navigation (current page highlighted), booking cards per `AdminDispatch.html`,
stepper with plain lines, labelled details list, actions row, mint confirmation bars, the Drivers and Calendar pages
in the same system, and the admin login. No logic changes.

Improvements (commit 2):

1. **Search and status filter**, client-side over the rendered cards: a search box (name, phone, reference, route)
   and chips All, New, Assigned, Confirmed, On trip. The sort dropdown stays server-side.
2. **Needs attention.** A summary strip (for example "2 new, 1 waiting for a driver, 1 pickup within 2 hours, 1
   details not sent") whose chips apply the filter, and an amber marker on cards that need action. Flags come from a
   pure helper `DashboardAttention` with an injectable clock (Malaysia time, UTC+8) and are exposed as new
   properties on `AdminBookingListItemViewModel`; `Index()` keeps its model type. Rules: New with no driver, "Assign
   a driver"; assignment Pending, "Waiting for driver"; Accepted and details not yet sent, "Send details to
   customer"; pickup within 2 hours and not yet picked up, "Pickup soon".
3. **Tap-to-call and WhatsApp** links on customer and driver phone numbers through a shared `PhoneLinks` helper
   (`tel:` and `https://wa.me/60...`). The Malaysian number normalisation that already exists in
   `WhatsAppCloudApiSender` is shared rather than duplicated.

## 3. Driver page

Look (commit 1): phone-first banner and location card (the existing `driver-location.js` hooks are kept), trip card
with the route drawn as pick-up (outlined dot) to drop-off (amber dot), limited details list, Accept and Reject
buttons, driver login.

Improvements (commit 2):

1. **One big next-step button** instead of the status dropdown for the normal flow: Confirmed becomes "Picked up",
   Picked_Up becomes "Start trip" (In_Transit), In_Transit becomes "Dropped off", Dropped_Off becomes "Complete
   trip". It posts to the existing `Driver/UpdateStatus(bookingId, newStatus)`, so the controller and server rules
   are unchanged. The mapping lives in a pure helper `DriverNextStep.For(status)`. The old dropdown remains under a
   collapsed "Something else?" so no capability is lost.
2. **Call and Maps.** `tel:` on the customer's phone; the pick-up and drop-off addresses link to
   `https://www.google.com/maps/search/?api=1&query=<encoded address>`, built by a tested helper.

## Components and boundaries

- `rideready-tokens.css` (design tokens) and `rideready.css` (page styles, tokens only, scoped under `body.rr-body`).
- Views: `_Layout`, `_PublicHeader`, per-page views and partials.
- Pure, unit-tested helpers: `BookingProgress` (exists), `DashboardAttention`, `DriverNextStep`, `PhoneLinks`,
  `MapsLink`.
- JavaScript stays inline per page and dependency-free.

## Testing and verification

For every commit: `dotnet build` and `dotnet test App` green (213 tests today); unit tests for every new helper and
for the validation messages; for each look-only commit, a before/after comparison of `asp-for`,
`asp-validation-for`, `name` and `id` attributes to prove nothing was removed; a local run against a throwaway
Postgres container with headless-Edge screenshots at 1440, 820 and 390 px, including a horizontal-overflow check;
screenshots saved under `RideReady Docs\Design Review\<phase>\`.

## Risks and notes

- Pre-selecting Car means the "vehicle type is required" error can no longer appear from the form; the server still
  enforces it.
- `localStorage` on shared computers: the saved details are only offered, never applied silently, and can be removed
  with **Forget me**.
- The client-side filter suits tens to hundreds of bookings; switch to server-side filtering if volume grows.
- Vehicle size hints are blocked until the owner supplies the capacities.
- Dark mode, removal of the unused old `ride-*` CSS, and dropping Bootstrap come last.

## Open items

- Passenger capacity for Car, Van and Bus (item 1.6).
- Confirm the "pickup within 2 hours" threshold for the needs-attention strip.
