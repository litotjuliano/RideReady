# Customer "driver details" message: full booking and driver details

## Goal
The message the admin sends from the dashboard button (`NotificationService.SendDriverDetailsToCustomerAsync`) becomes one complete confirmation: the full trip summary plus the driver's name, phone, vehicle and plate number. No other customer message changes.

## Message
```
Your RideReady booking RR-XXXX is confirmed.

Driver: Ah Seng.
Phone: 012-3456789.
Vehicle: Car ABC 1234.

Route: Puchong -> Ipoh.
Pick up: 20 Oct 2026, 5:00 AM · Bandar Kinrara, Puchong.
Drop-off (approx.): 20 Oct 2026, 12:33 PM · Pengkalan, Ipoh, Perak.
1 passenger · 1 bag · Car.
Fare: RM150.00 (Pay at Pickup).
```

- Sent by email (plain text) and WhatsApp with the same text.
- Each item is its own sentence, so it also reads correctly when the WhatsApp template flattens it to one line (no newlines allowed in a template parameter; max 1024 characters).
- "Phone" is shown once: drivers have a single phone number on file.
- Missing plate: "Vehicle: Car." (no gap). Missing drop-off time: the drop-off line shows the address only. Missing fare: no fare line.
- "Others" bookings read "Others: lorry", using the shared `TripText.Vehicle`.

## Code
- `TripText.When(date, time)` gives "20 Oct 2026, 5:00 AM"; `TripText.Passengers(passengers, bags, vehicle)` gives "1 passenger · 1 bag · Car".
- `NotificationService.SendDriverDetailsToCustomerAsync` builds the message from these.

## Tests
Extend the driver-details tests: driver name, phone, plate; route, pick up, drop-off, passengers line; Others description; no gaps when plate, drop-off or fare is missing; flattened WhatsApp text under 1024 characters.
