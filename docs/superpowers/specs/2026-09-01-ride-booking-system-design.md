# Ride Booking System - Complete Design Specification

**Project:** Ride Booking System (Vehicle Booking Platform)  
**Date:** 2026-09-01  
**Version:** 1.0.0  
**Author:** Architecture Design  
**Status:** Ready for Implementation

---

## 1. Executive Summary

This is a **standalone vehicle booking platform** for managing car, van, and bus bookings. The system enables customers to book rides through a web portal, allows admins/operators to manage bookings and assign drivers, and enables drivers to accept/reject assignments via a dedicated portal.

**Key Features:**
- Customer booking form with admin-only fare estimation
- Multi-stage admin dashboard for booking & driver management
- Driver acceptance/rejection workflow
- Multi-channel notifications (Email, WhatsApp, Google Calendar, SMS)
- Automated reminders for operators to prevent missed bookings
- Real-time driver location tracking via Google Maps
- Complete audit trail & booking history
- CI/CD pipeline with automatic semantic versioning
- PostgreSQL database with Entity Framework Core
- DigitalOcean deployment (develop → uat → staging → production)

---

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌──────────────────────────────────────────────────────┐
│              RIDE BOOKING SYSTEM                      │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ┌─────────────┐   ┌─────────────┐   ┌──────────┐   │
│  │  Customer   │   │   Admin     │   │  Driver  │   │
│  │ Web Portal  │   │ Dashboard   │   │  Portal  │   │
│  │  (MVC)      │   │  (MVC)      │   │ (MVC)    │   │
│  └──────┬──────┘   └──────┬──────┘   └────┬─────┘   │
│         │                 │               │         │
│         └─────────────────┼───────────────┘         │
│                           │                         │
│                  ┌────────▼────────┐                │
│                  │  ASP.NET Core   │                │
│                  │  MVC App +      │                │
│                  │  API Endpoints  │                │
│                  └────────┬────────┘                │
│                           │                         │
│         ┌─────────────────┼─────────────────┐       │
│         │                 │                 │       │
│    ┌────▼─────┐    ┌──────▼───┐    ┌───────▼──┐    │
│    │  Booking │    │  Driver  │    │Reminder  │    │
│    │ Service  │    │ Service  │    │ Service  │    │
│    └────┬─────┘    └────┬─────┘    └────┬────┘    │
│         │                │               │         │
│         └────────────────┼───────────────┘         │
│                          │                         │
│                  ┌───────▼──────┐                  │
│                  │  Event Bus   │                  │
│                  │  (Queue)     │                  │
│                  └───────┬──────┘                  │
│                          │                         │
│         ┌────────────────┼────────────────┐        │
│         │                │                │        │
│    ┌────▼────────┐ ┌─────▼──┐      ┌─────▼──┐    │
│    │Notification │ │Cron Job│      │Google  │    │
│    │Service      │ │Scheduler│      │Cal Sync│    │
│    │(Multi-      │ │(Reminders)    │Service │    │
│    │Channel)     │ └────────┘      └────────┘    │
│    └────┬────────┘                               │
│         │                                        │
│  ┌──────┴──────┬──────────┬─────────┐            │
│  │             │          │         │            │
│  ▼             ▼          ▼         ▼            │
│ Email      WhatsApp    Google    Push           │
│ SMTP         API      Calendar  Notif           │
│                                  (Future)       │
│                                                 │
│      ┌─────────────────────────┐                │
│      │  PostgreSQL Database    │                │
│      │  (Bookings, Drivers,    │                │
│      │   Events, Locations)    │                │
│      └─────────────────────────┘                │
│                                                 │
└──────────────────────────────────────────────────┘
```

### 2.2 Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | ASP.NET Core | 8.0 |
| Language | C# | 12.0 |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15+ |
| Frontend | Razor Views + Bootstrap 5 | 5.3 |
| Maps API | Google Maps | Latest |
| Email | SMTP | Standard |
| WhatsApp | WhatsApp Business API | Latest |
| Notifications | Quartz.NET | 3.6+ |
| Deployment | Docker | Latest |
| CI/CD | GitHub Actions | Latest |
| Versioning | Semantic Release | Auto |
| Cloud | DigitalOcean | Droplets + App Platform |

---

## 3. Core Components & Modules

### 3.1 Customer Booking Portal

**Purpose:** Allow customers to book rides with complete information capture

**User Flow:**
1. Customer enters personal info (name, phone, email)
2. Selects pickup & destination with autocomplete
3. Chooses date & time with interactive calendar UI
4. Specifies passengers & bags
5. Selects vehicle type (Car, Van, Bus)
6. Adds optional special notes
7. Reviews pricing quote
8. Confirms booking
9. Receives booking confirmation email + reference number

**Key Features:**
- Smart vehicle type auto-selection based on passenger count
- Real-time distance & duration calculation (Google Maps)
- Dynamic pricing quote before confirmation
- Booking history & status tracking
- Email notifications at every stage

---

### 3.2 Admin Dashboard

**Purpose:** Centralized control for operators to manage all bookings & drivers

**Key Screens:**

**Dashboard Home:**
- Today's metrics (total bookings, completed, pending, revenue)
- Driver availability status (available, on duty, offline)
- Real-time alerts for urgent assignments
- Live map with active drivers & bookings

**Booking Management:**
- List of all bookings with filterable/sortable columns
- Booking details modal (customer info, route, pricing, notes)
- Status update controls (Confirmed, Picked Up, In Transit, Dropped Off, Completed)
- Quick assignment interface (select driver from dropdown)
- Cancellation handling

**Driver Management:**
- List of all drivers with availability status
- Driver performance ratings & cancellation history
- Assign/reassign drivers to bookings
- Driver schedule view

**Calendar View:**
- Visual calendar of all upcoming bookings
- Google Calendar sync status for each booking
- Quick overview of daily load

**Reports & Analytics:**
- Booking trends (daily, weekly, monthly)
- Peak hours analysis
- Driver performance metrics
- Revenue tracking
- Customer ratings & feedback

---

### 3.3 Driver Portal

**Purpose:** Enable drivers to view assignments and update ride status

**Features:**
- Assigned bookings list (sorted by pickup time)
- Customer details & contact info
- Pickup location with Google Maps embed
- Accept/Reject assignment interface
- Real-time ride status updates:
  - Mark as Picked Up
  - Mark as In Transit
  - Mark as Dropped Off
  - Mark as Completed
- Ride history & earnings summary

---

## 4. Data Model (PostgreSQL)

### 4.1 Core Tables

**Customers Table:**
```sql
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    phone VARCHAR(20) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
```

**Drivers Table:**
```sql
CREATE TABLE drivers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    phone VARCHAR(20) NOT NULL UNIQUE,
    vehicle_type VARCHAR(50) NOT NULL,  -- Car, Van, Bus
    vehicle_number VARCHAR(50) UNIQUE,
    is_active BOOLEAN DEFAULT true,
    rating DECIMAL(3,2),
    cancellation_rate DECIMAL(5,2),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
```

**Bookings Table:**
```sql
CREATE TABLE bookings (
    id SERIAL PRIMARY KEY,
    booking_reference VARCHAR(50) NOT NULL UNIQUE,  -- RR-928653C2
    customer_id INT NOT NULL REFERENCES customers(id),
    pickup_location VARCHAR(255) NOT NULL,
    destination VARCHAR(255) NOT NULL,
    pickup_date DATE NOT NULL,
    pickup_time TIME NOT NULL,
    passengers INT NOT NULL,
    bags INT NOT NULL,
    requested_vehicle_type VARCHAR(50) NOT NULL,
    notes TEXT,
    status VARCHAR(50) DEFAULT 'New',  -- New, Confirmed, Driver_Assigned, Picked_Up, In_Transit, Dropped_Off, Completed, Cancelled, No_Show
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_bookings_status_date ON bookings(status, pickup_date);
```

**Driver Assignments Table:**
```sql
CREATE TABLE driver_assignments (
    id SERIAL PRIMARY KEY,
    booking_id INT NOT NULL REFERENCES bookings(id),
    driver_id INT NOT NULL REFERENCES drivers(id),
    assigned_at TIMESTAMP DEFAULT NOW(),
    accepted_at TIMESTAMP,
    rejected_at TIMESTAMP,
    assignment_status VARCHAR(50) DEFAULT 'Pending',  -- Pending, Accepted, Rejected
    UNIQUE(booking_id, driver_id)
);
```

**Booking Status History (Audit Trail):**
```sql
CREATE TABLE booking_status_history (
    id SERIAL PRIMARY KEY,
    booking_id INT NOT NULL REFERENCES bookings(id),
    previous_status VARCHAR(50),
    new_status VARCHAR(50) NOT NULL,
    changed_by VARCHAR(255),
    changed_at TIMESTAMP DEFAULT NOW()
);
```

**Booking Quotes Table:**
```sql
CREATE TABLE booking_quotes (
    id SERIAL PRIMARY KEY,
    booking_id INT REFERENCES bookings(id),
    base_fare DECIMAL(10,2),
    distance_km DECIMAL(10,2),
    distance_charge DECIMAL(10,2),
    duration_hours DECIMAL(10,2),
    time_charge DECIMAL(10,2),
    passenger_surcharge DECIMAL(10,2),
    luggage_fee DECIMAL(10,2),
    subtotal DECIMAL(10,2),
    service_tax DECIMAL(10,2),
    total_estimated_fare DECIMAL(10,2),
    actual_fare DECIMAL(10,2),
    payment_method VARCHAR(50),  -- Pay_at_Pickup, Bank_Transfer, Online_Payment
    created_at TIMESTAMP DEFAULT NOW()
);
```

**Pricing Settings Table:**
```sql
CREATE TABLE pricing_settings (
    id SERIAL PRIMARY KEY,
    vehicle_type VARCHAR(50) NOT NULL,  -- Car, Van, Bus
    base_fare DECIMAL(10,2) NOT NULL,
    per_km_rate DECIMAL(10,2) NOT NULL,
    per_hour_rate DECIMAL(10,2) NOT NULL,
    first_km_distance INT NOT NULL,
    first_km_charge DECIMAL(10,2),
    passenger_surcharge DECIMAL(10,2),
    service_tax_percent DECIMAL(5,2),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
```

**Notifications Table:**
```sql
CREATE TABLE notifications (
    id SERIAL PRIMARY KEY,
    booking_id INT NOT NULL REFERENCES bookings(id),
    recipient_type VARCHAR(50) NOT NULL,  -- Customer, Driver, Operator
    recipient_id INT,
    channel VARCHAR(50) NOT NULL,  -- Email, WhatsApp, SMS, Push
    event_type VARCHAR(100) NOT NULL,  -- BookingCreated, DriverAssigned, Reminder_1hr, etc.
    message_content TEXT,
    sent_at TIMESTAMP,
    delivery_status VARCHAR(50) DEFAULT 'Pending',  -- Pending, Sent, Failed
    error_message TEXT,
    retry_count INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW()
);
```

**Driver Locations Table:**
```sql
CREATE TABLE driver_locations (
    id SERIAL PRIMARY KEY,
    driver_id INT NOT NULL REFERENCES drivers(id),
    booking_id INT REFERENCES bookings(id),
    latitude DECIMAL(10, 8) NOT NULL,
    longitude DECIMAL(11, 8) NOT NULL,
    accuracy_meters INT,
    speed_kmh DECIMAL(5, 2),
    recorded_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_driver_locations_driver_booking 
ON driver_locations(driver_id, booking_id, recorded_at DESC);
```

**Operator Calendar Events Table:**
```sql
CREATE TABLE operator_calendar_events (
    id SERIAL PRIMARY KEY,
    booking_id INT NOT NULL REFERENCES bookings(id),
    google_event_id VARCHAR(255),
    synced_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW()
);
```

---

## 5. Customer Booking Form Specification

### 5.1 Form Fields & Validation

**Step 1: Customer Information**

| Field | Type | Validation | Required |
|-------|------|-----------|----------|
| Full Name | Text Input | Min 3, Max 100 chars | Yes |
| Phone | Tel Input | Format: +60XXXXXXXXX or 01X-XXXXXXXX | Yes |
| Email | Email Input | Valid email format | Yes |

**Step 2: Trip Details**

| Field | Type | Validation | Required |
|-------|------|-----------|----------|
| Pickup Location | Text + Autocomplete | Min 5 chars (Google Places) | Yes |
| Destination | Text + Autocomplete | Min 5 chars (Google Places) | Yes |
| Pickup Date | Calendar Picker | Today or future (max 30 days) | Yes |
| Pickup Time | Time Grid | 15-min intervals (6AM-12AM) | Yes |

**Step 3: Passenger & Vehicle**

| Field | Type | Validation | Required |
|-------|------|-----------|----------|
| Passengers | Number Spinner | Min 1, Max 8 | Yes |
| Bags | Number Spinner | Min 0, Max 10 | Yes |
| Vehicle Type | Dropdown | Car, Van, Bus (auto-filtered) | Yes |

**Step 4: Additional**

| Field | Type | Validation | Required |
|-------|------|-----------|----------|
| Special Notes | Textarea | Max 500 chars | No |

**Step 5: Review & Confirm**

- Review entered trip details (no fare/price shown — pricing is admin-only, see Section 6)
- Show payment method options (Pay at Pickup, Bank Transfer)
- Accept terms & conditions checkbox
- Confirm Booking button

---

### 5.2 Interactive Calendar UI

**Date Selection:**
- Calendar grid showing next 30 days
- Color coding: Blue (today), Green (available), Yellow (high-demand), Orange (few slots)
- Quick shortcuts: Today, Tomorrow, Next 7 Days
- Disable past dates

**Time Selection:**
- Time slots grouped by period: Morning (6AM-12PM), Afternoon (12PM-6PM), Evening (6PM-12AM)
- 15-minute intervals within each period
- Color: Green (available), Red (fully booked), Yellow (1-2 slots left)
- Display selected date/time summary

---

## 6. Pricing System

**Visibility:** Admin-only. The system calculates and stores a fare estimate for every booking, but this is never displayed to the customer during or after booking — only in the Admin Dashboard's booking details (Section 3.2), for the operator's reference when assigning a driver or reconciling payment.

### 6.1 Pricing Calculation Logic

**Formula:**
```
Total Fare = Base Fare 
           + Distance Charge (calculated from pickup to destination)
           + Time Charge (based on estimated duration)
           + Passenger Surcharge (for passengers beyond 1)
           + Luggage Fee (for bags beyond 2)
           + Service Tax (6% of subtotal)
```

**Example Calculation:**
```
Booking: KL Visa Center → Hyt Ipoh Office (215 km)
Passengers: 2, Bags: 2, Vehicle: Car

Base Fare (Car)           RM 50.00
Distance Charge (215 km)  RM 172.00  (0.80/km after first 10 km)
  ├─ First 10 km          RM 8.00
  └─ Remaining 205 km     RM 164.00
Time Charge (2.5 hrs)     RM 37.50  (15/hour)
Passenger Surcharge (1)   RM 5.00   (5 per extra passenger)
Luggage Fee (0 extra)     RM 0.00
──────────────────────────────────
Subtotal                  RM 264.50
Service Tax (6%)          RM 15.87
──────────────────────────────────
TOTAL ESTIMATED FARE      RM 280.37
```

### 6.2 Payment Methods (Manual for Now)

1. **Pay at Pickup** - Customer pays driver in cash/card
2. **Bank Transfer** - Customer pays to company account; admin verifies manually
3. **Online Payment** - Placeholder for future Stripe/PayPal integration

---

## 7. Multi-Channel Notification System

### 7.1 Notification Events & Recipients

**1. Booking Created**
- Customer: Email confirmation + booking reference
- Operator: Dashboard alert + Google Calendar event + Email

**2. Driver Assignment**
- Driver: WhatsApp notification (accept/reject link)
- Operator: Email confirmation
- Operator: Google Calendar updated

**3. Driver Accepted**
- Customer: Email + SMS (driver details, vehicle, contact)
- Operator: Dashboard update + Calendar event

**4. Reminders (Cron Jobs)**
- 1 Hour Before: Operator gets email + dashboard alert + Google Calendar reminder
- 30 Minutes Before: Escalation alert if driver not assigned
- At Pickup Time: Customer SMS reminder

**5. Ride Status Updates**
- Picked Up: Customer email, Operator dashboard
- In Transit: Customer email (optional), Operator update
- Completed: Customer email receipt + rating request

**6. Cancelled**
- Customer: Cancellation confirmation email
- Driver: WhatsApp cancellation alert
- Operator: Dashboard notification

### 7.2 Notification Channels

| Channel | Implementation | Status | Recipients |
|---------|---|---|---|
| Email | SMTP | Phase 1 | Customer, Operator |
| WhatsApp | WhatsApp Business API | Phase 1 | Driver, Operator |
| SMS | (Optional) | Phase 2 | Customer, Operator |
| Google Calendar | Google Calendar API + OAuth | Phase 1 | Operator |
| Push Notifications | Firebase | Phase 2 (Mobile App) | Driver, Customer |

### 7.3 Notification Retry Strategy

**Retry Delays:** 5 min → 15 min → 1 hr → 3 hrs  
**Max Retries:** 4  
**Dead Letter Queue:** After 5 failures, admin review required

**Fallback Strategy:**
- WhatsApp fails → Try SMS
- Email fails → Log to dead letter queue
- Google Calendar fails → Admin notified

---

## 8. Location & Geolocation Features

### 8.1 Google Maps Integration

**Features:**
- Real-time distance calculation (Directions API)
- ETA estimation based on current traffic
- Route visualization (polyline on map)
- Geolocation data storage for audit trail

**Admin Dashboard Map View:**
- Live map showing active drivers (blue pins)
- Customer pickups (green pins)
- Drop-off locations (red pins)
- Active booking routes (polylines)
- Zoom, pan, satellite view options

### 8.2 Driver Location Tracking

- Driver GPS coordinates stored every 30 seconds
- Real-time location broadcast to admin dashboard via SignalR
- ETA calculation to destination
- Geofencing alerts (future): Driver near pickup/dropoff

---

## 9. CI/CD Pipeline & Deployment

### 9.1 Multi-Environment Pipeline

**Environments:**
```
develop → uat → staging → production
  ↓       ↓       ↓          ↓
 Auto   Auto   Manual    Manual
Deploy  Deploy  Approve   Approve
```

**Branch Strategy:**
- `develop` → Development (auto-deploy)
- `uat` → UAT (auto-deploy)
- `staging` → Staging (manual approval)
- `main` → Production (manual approval + blue-green)

### 9.2 Automatic Semantic Versioning

**Commit Convention (Conventional Commits):**
```
feat: Add feature          → v1.0.0 → v1.1.0 (MINOR)
fix: Fix bug              → v1.0.0 → v1.0.1 (PATCH)
BREAKING CHANGE: ...      → v1.0.0 → v2.0.0 (MAJOR)
```

**Versioning Flow:**
1. Developer commits with conventional message
2. GitHub Actions runs semantic-release
3. Auto-detects version bump
4. Creates git tag (v1.0.0)
5. Generates release notes
6. Builds Docker image with version tag
7. Deploys to appropriate environment

**Docker Image Tags:**
```
ride-booking:v1.1.0      (Semantic version)
ride-booking:develop     (Branch name)
ride-booking:latest      (Most recent)
```

### 9.3 Deployment Strategy

**Development & UAT:** Auto-deploy on push  
**Staging:** Manual approval (final checks)  
**Production:** Blue-green deployment  
- Start new "green" container
- Health check
- If successful: switch traffic
- If failed: automatic rollback

### 9.4 Pre-Deployment Steps

1. Backup PostgreSQL database
2. Run migrations (Entity Framework Core)
3. Health check on new instance
4. Smoke tests
5. Notification to Slack

### 9.5 Post-Deployment

1. Monitor error logs
2. Track performance metrics
3. Version tagged in GitHub releases
4. Release notes auto-generated

---

## 10. Error Handling & Resilience

### 10.1 Notification Retries

**Dead Letter Queue:** Notifications that fail after 4 retries go to dead letter queue for admin manual review

**Fallback:**
- Email fails → Log to DLQ
- WhatsApp fails → Retry, then try SMS
- Google Calendar fails → Log and notify admin

### 10.2 Booking Status Consistency

**No-Show Detection (Cron Job):**
- If booking not marked "Picked Up" 30+ minutes after pickup time
- Auto-mark as "No_Show"
- Notify customer, driver, operator
- Apply cancellation fee

### 10.3 Driver Assignment Escalation

**Cron Job Alerts:**
- 2 hours before pickup: Check if driver assigned
- 1 hour before: Escalation alert to operator (email + dashboard + WhatsApp)
- 30 mins before: Urgent alert if still not assigned

---

## 11. Security & Data Protection

### 11.1 Authentication & Authorization

**Roles:**
- Customer (view own bookings only)
- Driver (view assigned bookings only)
- Operator (view all bookings, assign drivers)
- Admin (full system access)

### 11.2 Data Protection

- All inputs validated server-side
- Phone numbers hashed in logs (PII protection)
- Booking reference tokenized in URLs
- HTTPS/TLS for all communications
- Rate limiting: 100 req/min per user

### 11.3 Audit Trail

Complete logging of:
- Booking creation & status changes
- Driver assignments & rejections
- Payment verification
- User actions
- System errors

---

## 12. Testing Strategy

### 12.1 Unit Tests
- Price calculation accuracy
- Distance & ETA estimation
- Notification retry logic
- Status transition validation

### 12.2 Integration Tests
- Booking creation → Email sent
- Driver assignment → WhatsApp delivered
- Status update → Calendar synced
- Payment recorded

### 12.3 Load Tests
- 100 concurrent bookings
- 1000 simultaneous notifications
- Peak hour simulation

---

## 13. Analytics & Monitoring

### 13.1 Key Metrics

**Daily:**
- Total bookings
- Completed / Cancelled / No-show
- Total revenue
- Avg wait time for driver assignment
- Driver utilization rate

**Trends:**
- Peak hours & days
- Popular routes
- Vehicle type demand
- Customer ratings

### 13.2 Alerts

- Bookings < 1 hour from pickup (unassigned)
- Driver cancellation rate > 10%
- WhatsApp API failures
- Database connection issues
- Payment verification delays

---

## 14. Future Enhancements (Phase 2+)

- Mobile app for customers & drivers (using same API)
- Push notifications (Firebase)
- Driver ratings & reviews
- Surge pricing based on demand
- SMS notifications
- Geofencing alerts
- Scheduled recurring bookings
- Driver performance dashboard
- Advanced analytics & forecasting

---

## 15. Deployment Architecture (DigitalOcean)

### 15.1 Infrastructure Setup

**Droplets:**
- App Droplet: MVC app + background worker
- Database Droplet: PostgreSQL with automated backups
- (Optional) Separate droplet per environment

**Networks:**
- Private network between app & database
- SSH access via key-based auth
- Firewall rules: Only port 80/443 public

**Backups:**
- Daily automatic PostgreSQL backups
- 30-day retention
- Pre-deployment backup before each release

### 15.2 Monitoring

- Health checks on `/health` endpoint
- Error tracking (Sentry integration optional)
- Performance monitoring (New Relic optional)
- Log aggregation (optional)

---

## 16. Success Criteria

✅ Customers can book rides; admins can view the fare estimate for each booking  
✅ Operators receive notifications on multiple channels  
✅ Drivers accept/reject assignments within 5 minutes  
✅ Booking status tracked from creation to completion  
✅ Email notifications sent at all key stages  
✅ WhatsApp notifications to drivers & operators  
✅ Google Calendar synced for operator reminders  
✅ Zero-downtime deployments via blue-green  
✅ Automatic rollback on health check failure  
✅ Complete audit trail of all booking changes  

---

## 17. Non-Functional Requirements

| Requirement | Target |
|---|---|
| System Availability | 99.5% uptime |
| Response Time (Web) | < 2 seconds |
| Email Delivery | < 5 minutes |
| WhatsApp Delivery | < 30 seconds |
| Database Query | < 100ms (95th percentile) |
| Concurrent Users | 500+ simultaneous |
| Booking Creation | < 2 seconds |
| Driver Assignment | < 10 seconds |

---

## 18. Implementation Roadmap

**Phase 1 (MVP - Weeks 1-4):**
- ✅ Database schema (PostgreSQL)
- ✅ Customer booking form & portal
- ✅ Admin dashboard (basic)
- ✅ Driver portal (basic)
- ✅ Pricing calculation
- ✅ Email notifications
- ✅ WhatsApp API integration
- ✅ Google Calendar sync
- ✅ Basic CI/CD pipeline

**Phase 2 (Enhancement - Weeks 5-8):**
- Real-time driver tracking (Google Maps)
- Advanced analytics dashboard
- SMS notifications
- Performance optimization
- Load testing & scaling

**Phase 3 (Mobile - Weeks 9-12):**
- Customer mobile app
- Driver mobile app
- Push notifications
- Offline mode

---

## 19. Glossary

| Term | Definition |
|------|-----------|
| Booking Reference | Unique identifier (e.g., RR-928653C2) |
| Operator | Admin user managing bookings |
| Driver Assignment | Process of assigning a booking to a driver |
| Status | Current state of booking (New, Confirmed, etc.) |
| ETA | Estimated Time of Arrival |
| Dead Letter Queue | Queue for failed notifications requiring review |
| Blue-Green Deployment | Zero-downtime deployment strategy |
| Semantic Versioning | Version format: Major.Minor.Patch |

---

---

## 20. UI/Visual Design Reference

A working prototype ("RideReady") was reviewed for visual style only — its functional flows (no pricing shown, WhatsApp-only driver assignment, ad-hoc drivers, no accounts) do **not** apply; the system continues to follow Sections 3-11 above. Razor views for the Customer Booking Portal, Admin Dashboard, and Driver Portal should be styled with Bootstrap 5 + custom CSS to approximate this look, not by adopting Tailwind.

### 20.1 Color Palette

| Role | Hex | Usage |
|---|---|---|
| Primary dark green | `#173f2b` | Headers, primary buttons |
| Accent green (mid) | `#20653d` | Links, hover states |
| Accent green (bright) | `#2b7a4b` | Badges, secondary accents |
| Accent green (vivid) | `#1f9d55` | CTA highlights |
| WhatsApp green | `#25D366` | WhatsApp-related actions only |
| Background tint 1 | `#f4f7f3` | Page background (customer-facing) |
| Background tint 2 | `#eef3ef` | Page background (admin/driver) |
| Background tint 3 | `#e8f4eb` | Badge/card fill |
| Background tint 4 | `#dff3e5` | Pill/tag fill |
| Border | `#dce5de` / `#d7e2da` / `#cbdacf` | Card and input borders |
| Text primary | `#132219` | Headings, body text |
| Text muted | `#5b6960` / `#68756d` | Secondary/helper text |

### 20.2 Layout Patterns

- Cards use large border radii (16-28px) with soft box-shadows, not sharp edges or heavy borders
- Buttons and status badges are pill-shaped (`border-radius: 999px`), bold, uppercase for badges
- Admin dashboard booking queue renders as a vertical list of cards, not a dense data-grid table
- Customer booking page uses a two-column layout: marketing copy/steps on the left, the form in a sticky card on the right (collapses to single column on mobile)
- Header bars are simple: a colored square logo mark + brand wordmark, with nav links right-aligned
- Status indicators are small, uppercase, bold pill badges with a tinted background matching the status

### 20.3 Typography

The prototype uses Geist/Geist Mono. Since the project uses Bootstrap 5 rather than Tailwind, substitute a comparable clean sans-serif (e.g. Inter via Google Fonts) for headings and body text, keeping the same visual weight and letter-spacing feel.

---

**Document Version:** 1.2.0  
**Last Updated:** 2026-09-01  
**Next Review:** After Phase 1 completion
