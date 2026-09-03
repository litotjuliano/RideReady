# Ride Booking System Phase 1 (MVP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a functional vehicle booking platform with customer booking, admin management, driver portal, multi-channel notifications, and CI/CD pipeline.

**Architecture:** ASP.NET Core 8 MVC monolith with PostgreSQL, background workers for async notifications, event-driven architecture for loose coupling between services.

**Tech Stack:** ASP.NET Core 8, Entity Framework Core 8, PostgreSQL 15, Bootstrap 5, Google Maps API, WhatsApp Business API, Google Calendar API, Quartz.NET, Docker, GitHub Actions

## Global Constraints

- .NET 8.0 minimum (C# 12)
- PostgreSQL 15+
- All currency in Malaysian Ringgit (RM)
- Date format: MMM dd, yyyy (e.g., Sep 01, 2026)
- Phone format: +60XXXXXXXXX or 01X-XXXXXXXX (Malaysia)
- All DB operations async/await
- No domain models exposed to views (use ViewModels)
- Data validation: Data Annotations + server-side checks
- Dependency injection for all services
- No payment integration (Phase 1 is manual only)

---

## Part 1: Project Setup & Database Foundation

### Task 1: Create ASP.NET Core MVC Project Structure

**Files:**
- Create: `RideBooking.csproj`
- Create: `Program.cs`
- Create: `appsettings.json`
- Create: `.gitignore`
- Create: `Dockerfile`

**Interfaces:**
- Produces: Base project with NuGet packages, dependency injection configured

- [ ] **Step 1: Create project directory and initialize git**

```bash
cd "/Users/litojuliano/LitXus System/Ride"
dotnet new globaljson --sdk-version 8.0.401 --roll-forward latestMinor
git init
```

- [ ] **Step 2: Create ASP.NET Core MVC project**

```bash
dotnet new mvc -n RideBooking -f net8.0
cd RideBooking
```

- [ ] **Step 3: Add required NuGet packages**

```bash
dotnet add package Microsoft.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package Quartz.Extensions.Hosting --version 3.6.2
dotnet add package SendGrid --version 9.28.1
dotnet add package Twilio --version 6.3.0
dotnet add package Google.Apis.Calendar.v3 --version 1.60.0.3142
dotnet add package MailKit --version 4.3.0
```

- [ ] **Step 4: Configure appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ride_booking;Username=rideuser;Password=your_password"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "noreply@ridebooking.my",
    "SenderName": "Ride Booking System"
  },
  "WhatsAppSettings": {
    "ApiUrl": "https://graph.instagram.com/v18.0",
    "BusinessAccountId": "YOUR_WHATSAPP_BUSINESS_ID",
    "AccessToken": "YOUR_ACCESS_TOKEN",
    "PhoneNumberId": "YOUR_PHONE_NUMBER_ID"
  },
  "GoogleMapsSettings": {
    "ApiKey": "YOUR_GOOGLE_MAPS_API_KEY"
  },
  "GoogleCalendarSettings": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5000/auth/google/callback"
  }
}
```

- [ ] **Step 5: Create .gitignore**

```
bin/
obj/
.vs/
*.user
appsettings.*.json
*.db
*.log
node_modules/
```

- [ ] **Step 6: Create Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /build
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore
RUN dotnet publish --configuration Release --no-build -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=builder /app .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "RideBooking.dll"]
```

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: initialize ASP.NET Core 8 MVC project with dependencies"
```

---

### Task 2: Create PostgreSQL Database Schema

**Files:**
- Create: `Data/RideBookingDbContext.cs`
- Create: `Data/Migrations/001_InitialCreate.cs`
- Create: `Models/Customer.cs`
- Create: `Models/Driver.cs`
- Create: `Models/Booking.cs`
- Create: `Models/DriverAssignment.cs`
- Create: `Models/BookingStatusHistory.cs`
- Create: `Models/BookingQuote.cs`
- Create: `Models/PricingSetting.cs`
- Create: `Models/Notification.cs`
- Create: `Models/DriverLocation.cs`
- Create: `Models/OperatorCalendarEvent.cs`

**Interfaces:**
- Produces: DbContext with all entities, migrations scaffolded

- [ ] **Step 1: Create Customer model**

```csharp
// Models/Customer.cs
namespace RideBooking.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
```

- [ ] **Step 2: Create Driver model**

```csharp
// Models/Driver.cs
namespace RideBooking.Models
{
    public class Driver
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty; // Car, Van, Bus
        public string? VehicleNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public decimal? Rating { get; set; }
        public decimal CancellationRate { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<DriverAssignment> Assignments { get; set; } = new List<DriverAssignment>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
    }
}
```

- [ ] **Step 3: Create Booking model**

```csharp
// Models/Booking.cs
namespace RideBooking.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string RequestedVehicleType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Status { get; set; } = "New"; // New, Confirmed, Driver_Assigned, Picked_Up, In_Transit, Dropped_Off, Completed, Cancelled, No_Show
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DriverAssignment? CurrentAssignment { get; set; }
        public BookingQuote? Quote { get; set; }
        public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
```

- [ ] **Step 4: Create DriverAssignment model**

```csharp
// Models/DriverAssignment.cs
namespace RideBooking.Models
{
    public class DriverAssignment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string AssignmentStatus { get; set; } = "Pending"; // Pending, Accepted, Rejected
    }
}
```

- [ ] **Step 5: Create BookingStatusHistory model**

```csharp
// Models/BookingStatusHistory.cs
namespace RideBooking.Models
{
    public class BookingStatusHistory
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 6: Create BookingQuote model**

```csharp
// Models/BookingQuote.cs
namespace RideBooking.Models
{
    public class BookingQuote
    {
        public int Id { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public decimal BaseFare { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal DistanceCharge { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TimeCharge { get; set; }
        public decimal PassengerSurcharge { get; set; }
        public decimal LuggageFee { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ServiceTax { get; set; }
        public decimal TotalEstimatedFare { get; set; }
        public decimal? ActualFare { get; set; }
        public string PaymentMethod { get; set; } = string.Empty; // Pay_at_Pickup, Bank_Transfer
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 7: Create PricingSetting model**

```csharp
// Models/PricingSetting.cs
namespace RideBooking.Models
{
    public class PricingSetting
    {
        public int Id { get; set; }
        public string VehicleType { get; set; } = string.Empty; // Car, Van, Bus
        public decimal BaseFare { get; set; }
        public decimal PerKmRate { get; set; }
        public decimal PerHourRate { get; set; }
        public int FirstKmDistance { get; set; }
        public decimal? FirstKmCharge { get; set; }
        public decimal? PassengerSurcharge { get; set; }
        public decimal ServiceTaxPercent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 8: Create Notification model**

```csharp
// Models/Notification.cs
namespace RideBooking.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string RecipientType { get; set; } = string.Empty; // Customer, Driver, Operator
        public int? RecipientId { get; set; }
        public string Channel { get; set; } = string.Empty; // Email, WhatsApp, SMS, Push
        public string EventType { get; set; } = string.Empty;
        public string? MessageContent { get; set; }
        public DateTime? SentAt { get; set; }
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Failed
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 9: Create DriverLocation model**

```csharp
// Models/DriverLocation.cs
namespace RideBooking.Models
{
    public class DriverLocation
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public int? BookingId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int? AccuracyMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 10: Create OperatorCalendarEvent model**

```csharp
// Models/OperatorCalendarEvent.cs
namespace RideBooking.Models
{
    public class OperatorCalendarEvent
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string? GoogleEventId { get; set; }
        public DateTime? SyncedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 11: Create RideBookingDbContext**

```csharp
// Data/RideBookingDbContext.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Models;

namespace RideBooking.Data
{
    public class RideBookingDbContext : DbContext
    {
        public RideBookingDbContext(DbContextOptions<RideBookingDbContext> options) 
            : base(options) { }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<DriverAssignment> DriverAssignments => Set<DriverAssignment>();
        public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
        public DbSet<BookingQuote> BookingQuotes => Set<BookingQuote>();
        public DbSet<PricingSetting> PricingSettings => Set<PricingSetting>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
        public DbSet<OperatorCalendarEvent> OperatorCalendarEvents => Set<OperatorCalendarEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes
            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.Status, b.PickupDate });
            
            modelBuilder.Entity<DriverLocation>()
                .HasIndex(dl => new { dl.DriverId, dl.BookingId, dl.RecordedAt })
                .IsDescending(false, false, true);
            
            modelBuilder.Entity<DriverAssignment>()
                .HasIndex(da => new { da.BookingId, da.DriverId })
                .IsUnique();

            // Relationships
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingQuote>()
                .HasOne(bq => bq.Booking)
                .WithOne(b => b.Quote)
                .HasForeignKey<BookingQuote>(bq => bq.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 12: Create initial migration**

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

- [ ] **Step 13: Commit**

```bash
git add Models/ Data/
git commit -m "feat: create PostgreSQL schema with Entity Framework models"
```

---

## Part 2: Customer Booking Portal

### Task 3: Create Booking ViewModel & Service

**Files:**
- Create: `ViewModels/BookingRequestViewModel.cs`
- Create: `ViewModels/BookingQuoteViewModel.cs`
- Create: `Services/BookingService.cs`
- Create: `Services/IBookingService.cs`
- Create: `Tests/Services/BookingServiceTests.cs`

**Interfaces:**
- Consumes: RideBookingDbContext, PricingSetting
- Produces: `IBookingService` with `CreateBookingAsync(request)`, `GetQuoteAsync(request)`, `GetBookingByReferenceAsync(ref)`

- [ ] **Step 1: Create BookingRequestViewModel**

```csharp
// ViewModels/BookingRequestViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class BookingRequestViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(255, MinimumLength = 3)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60|0)[0-9]{9,10}$", 
            ErrorMessage = "Invalid Malaysian phone number")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup location is required")]
        [StringLength(255, MinimumLength = 5)]
        public string PickupLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Destination is required")]
        [StringLength(255, MinimumLength = 5)]
        public string Destination { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup date is required")]
        public DateOnly PickupDate { get; set; }

        [Required(ErrorMessage = "Pickup time is required")]
        public TimeOnly PickupTime { get; set; }

        [Required(ErrorMessage = "Passengers count is required")]
        [Range(1, 8)]
        public int Passengers { get; set; }

        [Required(ErrorMessage = "Bags count is required")]
        [Range(0, 10)]
        public int Bags { get; set; }

        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
```

- [ ] **Step 2: Create BookingQuoteViewModel**

```csharp
// ViewModels/BookingQuoteViewModel.cs
namespace RideBooking.ViewModels
{
    public class BookingQuoteViewModel
    {
        public decimal BaseFare { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal DistanceCharge { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TimeCharge { get; set; }
        public decimal PassengerSurcharge { get; set; }
        public decimal LuggageFee { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ServiceTax { get; set; }
        public decimal TotalEstimatedFare { get; set; }
        public string EstimatedDuration { get; set; } = string.Empty;
        public List<string> PaymentMethods { get; set; } = new();
    }
}
```

- [ ] **Step 3: Write failing test for BookingService**

```csharp
// Tests/Services/BookingServiceTests.cs
using Xunit;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.Data;
using Microsoft.EntityFrameworkCore;
using RideBooking.ViewModels;

namespace RideBooking.Tests.Services
{
    public class BookingServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        [Fact]
        public async Task CreateBooking_WithValidRequest_ReturnsBookingWithReference()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new BookingService(context);
            var request = new BookingRequestViewModel
            {
                CustomerName = "Uncle Sim",
                CustomerPhone = "0125183838",
                CustomerEmail = "sim@email.com",
                PickupLocation = "KL Visa Center",
                Destination = "Hyt Ipoh Office",
                PickupDate = new DateOnly(2026, 8, 27),
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car"
            };

            // Act
            var booking = await service.CreateBookingAsync(request);

            // Assert
            Assert.NotNull(booking);
            Assert.NotEmpty(booking.BookingReference);
            Assert.StartsWith("RR-", booking.BookingReference);
            Assert.Equal("New", booking.Status);
        }

        [Fact]
        public async Task GetQuote_WithValidRequest_CalculatesPricingCorrectly()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedPricingSettings(context);
            var service = new BookingService(context);
            var request = new BookingRequestViewModel
            {
                CustomerName = "Uncle Sim",
                CustomerPhone = "0125183838",
                CustomerEmail = "sim@email.com",
                PickupLocation = "KL Visa Center",
                Destination = "Hyt Ipoh Office",
                PickupDate = new DateOnly(2026, 8, 27),
                PickupTime = new TimeOnly(13, 8),
                Passengers = 2,
                Bags = 2,
                VehicleType = "Car"
            };

            // Act
            var quote = await service.GetQuoteAsync(request);

            // Assert
            Assert.NotNull(quote);
            Assert.True(quote.TotalEstimatedFare > 0);
        }

        private async Task SeedPricingSettings(RideBookingDbContext context)
        {
            context.PricingSettings.Add(new PricingSetting
            {
                VehicleType = "Car",
                BaseFare = 50m,
                PerKmRate = 0.80m,
                PerHourRate = 15m,
                FirstKmDistance = 10,
                FirstKmCharge = 8m,
                PassengerSurcharge = 5m,
                ServiceTaxPercent = 6m
            });
            await context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

```bash
cd RideBooking
dotnet test Tests/Services/BookingServiceTests.cs -v
```

Expected: FAIL (BookingService class not found)

- [ ] **Step 5: Create BookingService implementation**

```csharp
// Services/IBookingService.cs
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IBookingService
    {
        Task<Booking> CreateBookingAsync(BookingRequestViewModel request);
        Task<BookingQuoteViewModel> GetQuoteAsync(BookingRequestViewModel request);
        Task<Booking?> GetBookingByReferenceAsync(string reference);
    }
}
```

```csharp
// Services/BookingService.cs
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace RideBooking.Services
{
    public class BookingService : IBookingService
    {
        private readonly RideBookingDbContext _context;
        private readonly ILocationService _locationService;

        public BookingService(RideBookingDbContext context, ILocationService? locationService = null)
        {
            _context = context;
            _locationService = locationService ?? new MockLocationService();
        }

        public async Task<Booking> CreateBookingAsync(BookingRequestViewModel request)
        {
            // Create or get customer
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == request.CustomerPhone);
            if (customer == null)
            {
                customer = new Customer
                {
                    Name = request.CustomerName,
                    Phone = request.CustomerPhone,
                    Email = request.CustomerEmail
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // Create booking
            var booking = new Booking
            {
                BookingReference = GenerateBookingReference(),
                CustomerId = customer.Id,
                PickupLocation = request.PickupLocation,
                Destination = request.Destination,
                PickupDate = request.PickupDate,
                PickupTime = request.PickupTime,
                Passengers = request.Passengers,
                Bags = request.Bags,
                RequestedVehicleType = request.VehicleType,
                Notes = request.Notes,
                Status = "New"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Create quote
            var quote = await GetQuoteAsync(request);
            var quoteEntity = new BookingQuote
            {
                BookingId = booking.Id,
                BaseFare = quote.BaseFare,
                DistanceKm = quote.DistanceKm,
                DistanceCharge = quote.DistanceCharge,
                DurationHours = quote.DurationHours,
                TimeCharge = quote.TimeCharge,
                PassengerSurcharge = quote.PassengerSurcharge,
                LuggageFee = quote.LuggageFee,
                Subtotal = quote.Subtotal,
                ServiceTax = quote.ServiceTax,
                TotalEstimatedFare = quote.TotalEstimatedFare,
                PaymentMethod = "Pay_at_Pickup"
            };
            _context.BookingQuotes.Add(quoteEntity);
            await _context.SaveChangesAsync();

            return booking;
        }

        public async Task<BookingQuoteViewModel> GetQuoteAsync(BookingRequestViewModel request)
        {
            var pricing = await _context.PricingSettings
                .FirstOrDefaultAsync(p => p.VehicleType == request.VehicleType && p.IsActive);
            
            if (pricing == null)
                throw new InvalidOperationException($"Pricing not configured for {request.VehicleType}");

            var distance = await _locationService.GetDistanceAsync(request.PickupLocation, request.Destination);
            var duration = await _locationService.GetDurationAsync(request.PickupLocation, request.Destination);

            var baseFare = pricing.BaseFare;
            var distanceCharge = CalculateDistanceCharge(distance, pricing);
            var timeCharge = duration * pricing.PerHourRate;
            var passengerSurcharge = Math.Max(0, request.Passengers - 1) * (pricing.PassengerSurcharge ?? 0);
            var luggageFee = Math.Max(0, request.Bags - 2) * 5m;
            var subtotal = baseFare + distanceCharge + timeCharge + passengerSurcharge + luggageFee;
            var serviceTax = subtotal * (pricing.ServiceTaxPercent / 100);

            return new BookingQuoteViewModel
            {
                BaseFare = baseFare,
                DistanceKm = distance,
                DistanceCharge = distanceCharge,
                DurationHours = duration,
                TimeCharge = timeCharge,
                PassengerSurcharge = passengerSurcharge,
                LuggageFee = luggageFee,
                Subtotal = subtotal,
                ServiceTax = serviceTax,
                TotalEstimatedFare = subtotal + serviceTax,
                EstimatedDuration = FormatDuration(duration),
                PaymentMethods = new List<string> { "Pay_at_Pickup", "Bank_Transfer" }
            };
        }

        public async Task<Booking?> GetBookingByReferenceAsync(string reference)
        {
            return await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Quote)
                .FirstOrDefaultAsync(b => b.BookingReference == reference);
        }

        private decimal CalculateDistanceCharge(decimal distanceKm, PricingSetting pricing)
        {
            if (distanceKm <= pricing.FirstKmDistance)
                return pricing.FirstKmCharge ?? 0;

            var firstKmCharge = pricing.FirstKmCharge ?? 0;
            var remainingKm = distanceKm - pricing.FirstKmDistance;
            var remainingCharge = remainingKm * pricing.PerKmRate;
            return firstKmCharge + remainingCharge;
        }

        private string GenerateBookingReference()
        {
            var timestamp = DateTime.UtcNow.Ticks.ToString().TakeLast(6);
            var random = new Random().Next(1000, 9999).ToString("X");
            return $"RR-{string.Concat(timestamp)}{random}".ToUpper();
        }

        private string FormatDuration(decimal hours)
        {
            var totalMinutes = (int)(hours * 60);
            var hrs = totalMinutes / 60;
            var mins = totalMinutes % 60;
            return $"{hrs}h {mins}m";
        }
    }
}
```

- [ ] **Step 6: Create ILocationService (mock for now)**

```csharp
// Services/ILocationService.cs
namespace RideBooking.Services
{
    public interface ILocationService
    {
        Task<decimal> GetDistanceAsync(string pickup, string destination);
        Task<decimal> GetDurationAsync(string pickup, string destination);
    }

    public class MockLocationService : ILocationService
    {
        public Task<decimal> GetDistanceAsync(string pickup, string destination)
        {
            return Task.FromResult(215m); // Mock: KL to Ipoh is ~215km
        }

        public Task<decimal> GetDurationAsync(string pickup, string destination)
        {
            return Task.FromResult(2.5m); // Mock: 2.5 hours
        }
    }
}
```

- [ ] **Step 7: Run test to verify it passes**

```bash
dotnet test Tests/Services/BookingServiceTests.cs -v
```

Expected: PASS

- [ ] **Step 8: Register service in Program.cs**

```csharp
// Program.cs
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ILocationService, GoogleMapsLocationService>(); // To be implemented later
```

- [ ] **Step 9: Commit**

```bash
git add Services/ ViewModels/ Tests/
git commit -m "feat: implement booking service with pricing calculation"
```

---

### Task 4: Customer Booking Form (Controller + Views)

**Files:**
- Modify: `ViewModels/BookingRequestViewModel.cs`
- Modify: `Services/BookingService.cs`
- Modify: `Tests/Services/BookingServiceTests.cs`
- Create: `Controllers/BookingController.cs`
- Create: `Tests/Controllers/BookingControllerTests.cs`
- Create: `Views/Booking/Create.cshtml`
- Create: `Views/Booking/Confirmation.cshtml`
- Modify: `wwwroot/css/site.css`
- Modify: `Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IBookingService.CreateBookingAsync`
- Produces: `GET/POST /Booking/Create`, `GET /Booking/Confirmation`

> **Note on pricing:** per spec §6, the fare estimate is admin-only. This form never displays `BookingQuoteViewModel` to the customer — `IBookingService.CreateBookingAsync` already calculates and stores it internally (Task 3), unchanged here.

- [ ] **Step 1: Add PaymentMethod and AcceptedTerms to the booking request**

```csharp
// ViewModels/BookingRequestViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class BookingRequestViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 3)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Invalid Malaysian phone number. Use +60XXXXXXXXX or 01X-XXXXXXXX format")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup location is required")]
        [StringLength(255, MinimumLength = 5)]
        public string PickupLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Destination is required")]
        [StringLength(255, MinimumLength = 5)]
        public string Destination { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup date is required")]
        public DateOnly PickupDate { get; set; }

        [Required(ErrorMessage = "Pickup time is required")]
        public TimeOnly PickupTime { get; set; }

        [Required(ErrorMessage = "Passengers count is required")]
        [Range(1, 8)]
        public int Passengers { get; set; }

        [Required(ErrorMessage = "Bags count is required")]
        [Range(0, 10)]
        public int Bags { get; set; }

        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Payment method is required")]
        [RegularExpression("^(Pay_at_Pickup|Bank_Transfer)$")]
        public string PaymentMethod { get; set; } = "Pay_at_Pickup";

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions")]
        public bool AcceptedTerms { get; set; }
    }
}
```

- [ ] **Step 2: Use the selected payment method instead of a hardcoded value**

In `Services/BookingService.cs`, find the `CreateBookingAsync` method and change the quote entity's `PaymentMethod` assignment:

```csharp
// Before:
                var quoteEntity = new BookingQuote
                {
                    BookingId = booking.Id,
                    BaseFare = quote.BaseFare,
                    DistanceKm = quote.DistanceKm,
                    DistanceCharge = quote.DistanceCharge,
                    DurationHours = quote.DurationHours,
                    TimeCharge = quote.TimeCharge,
                    PassengerSurcharge = quote.PassengerSurcharge,
                    LuggageFee = quote.LuggageFee,
                    Subtotal = quote.Subtotal,
                    ServiceTax = quote.ServiceTax,
                    TotalEstimatedFare = quote.TotalEstimatedFare,
                    PaymentMethod = "Pay_at_Pickup"
                };

// After:
                var quoteEntity = new BookingQuote
                {
                    BookingId = booking.Id,
                    BaseFare = quote.BaseFare,
                    DistanceKm = quote.DistanceKm,
                    DistanceCharge = quote.DistanceCharge,
                    DurationHours = quote.DurationHours,
                    TimeCharge = quote.TimeCharge,
                    PassengerSurcharge = quote.PassengerSurcharge,
                    LuggageFee = quote.LuggageFee,
                    Subtotal = quote.Subtotal,
                    ServiceTax = quote.ServiceTax,
                    TotalEstimatedFare = quote.TotalEstimatedFare,
                    PaymentMethod = request.PaymentMethod
                };
```

- [ ] **Step 3: Update existing service tests for the new required fields**

In `Tests/Services/BookingServiceTests.cs`, add `PaymentMethod = "Pay_at_Pickup"` and `AcceptedTerms = true` to both `BookingRequestViewModel` instances (in `CreateBooking_WithValidRequest_ReturnsBookingWithReference` and `GetQuote_WithValidRequest_CalculatesPricingCorrectly`), then add one assertion to the first test:

```csharp
            // Assert
            Assert.NotNull(booking);
            Assert.NotEmpty(booking.BookingReference);
            Assert.StartsWith("RR-", booking.BookingReference);
            Assert.Equal("New", booking.Status);
            Assert.Equal("Pay_at_Pickup", booking.Quote?.PaymentMethod);
```

Note: `CreateBookingAsync` doesn't currently load `Quote` on the returned `booking` — change the last line of `CreateBookingAsync` in `Services/BookingService.cs` from `return booking;` to re-fetch with the quote included:

```csharp
                await transaction.CommitAsync();
                return await _context.Bookings
                    .Include(b => b.Quote)
                    .FirstAsync(b => b.Id == booking.Id);
```

- [ ] **Step 4: Run the service tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~BookingServiceTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Write a failing test for successful booking creation via the controller**

```csharp
// Tests/Controllers/BookingControllerTests.cs
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Tests.Controllers
{
    public class BookingControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<RideBookingDbContext> GetSeededDbContextAsync()
        {
            var context = GetInMemoryDbContext();
            context.PricingSettings.Add(new PricingSetting
            {
                VehicleType = "Car",
                BaseFare = 50m,
                PerKmRate = 0.80m,
                PerHourRate = 15m,
                FirstKmDistance = 10,
                FirstKmCharge = 8m,
                PassengerSurcharge = 5m,
                ServiceTaxPercent = 6m
            });
            await context.SaveChangesAsync();
            return context;
        }

        private static BookingRequestViewModel ValidRequest() => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            CustomerEmail = "sim@email.com",
            PickupLocation = "KL Visa Center",
            Destination = "Hyt Ipoh Office",
            PickupDate = new DateOnly(2026, 9, 5),
            PickupTime = new TimeOnly(13, 8),
            Passengers = 2,
            Bags = 2,
            VehicleType = "Car",
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        [Fact]
        public async Task Create_Post_WithValidRequest_RedirectsToConfirmation()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service)
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
            };

            // Act
            var result = await controller.Create(ValidRequest());

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
            Assert.NotNull(controller.TempData["BookingReference"]);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~BookingControllerTests`
Expected: FAIL (`BookingController` does not exist, and `Mock` is not available — remove the `Mock.Of<...>` usage before compiling; see Step 7's corrected test)

- [ ] **Step 7: Replace the TempData provider stub with a minimal fake (no mocking library is installed)**

Replace the `Mock.Of<...>()` call in Step 5's test with a tiny local fake, since the project has no Moq dependency:

```csharp
// Add near the top of Tests/Controllers/BookingControllerTests.cs, inside the namespace, above the class:
    internal class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }
```

Then in `Create_Post_WithValidRequest_RedirectsToConfirmation`, replace:

```csharp
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
```

with:

```csharp
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
```

Run: `dotnet test --filter FullyQualifiedName~BookingControllerTests`
Expected: FAIL (`BookingController` does not exist)

- [ ] **Step 8: Implement BookingController**

```csharp
// Controllers/BookingController.cs
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new BookingRequestViewModel
            {
                PickupDate = DateOnly.FromDateTime(DateTime.Today)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingRequestViewModel request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            try
            {
                var booking = await _bookingService.CreateBookingAsync(request);
                TempData["BookingReference"] = booking.BookingReference;
                return RedirectToAction(nameof(Confirmation));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(request);
            }
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            var reference = TempData["BookingReference"] as string;
            if (string.IsNullOrEmpty(reference))
            {
                return RedirectToAction(nameof(Create));
            }

            ViewBag.BookingReference = reference;
            return View();
        }
    }
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~BookingControllerTests`
Expected: PASS

- [ ] **Step 10: Write and verify tests for invalid submissions**

Add two more tests to `Tests/Controllers/BookingControllerTests.cs`:

```csharp
        [Fact]
        public async Task Create_Post_WithPastPickupDate_ReturnsViewWithError()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service)
            {
                TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                    new NullTempDataProvider())
            };
            var request = ValidRequest();
            request.PickupDate = new DateOnly(2020, 1, 1);
            request.PickupTime = new TimeOnly(9, 0);

            // Act
            var result = await controller.Create(request);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
        }

        [Fact]
        public async Task Create_Post_WithInvalidModelState_ReturnsViewWithSameModel()
        {
            // Arrange
            var context = await GetSeededDbContextAsync();
            var service = new BookingService(context);
            var controller = new BookingController(service);
            controller.ModelState.AddModelError("CustomerName", "Name is required");
            var request = ValidRequest();
            request.CustomerName = string.Empty;

            // Act
            var result = await controller.Create(request);

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(request, view.Model);
        }
```

Run: `dotnet test --filter FullyQualifiedName~BookingControllerTests`
Expected: PASS (4 tests total)

- [ ] **Step 11: Create the booking form view**

```html
@* Views/Booking/Create.cshtml *@
@model RideBooking.ViewModels.BookingRequestViewModel
@{
    ViewData["Title"] = "Book a Ride";
    var today = DateTime.Today.ToString("yyyy-MM-dd");
    var maxDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
}

<div class="ride-hero">
    <div class="row g-4">
        <div class="col-lg-5">
            <p class="ride-eyebrow">Easy local transport</p>
            <h1 class="ride-heading">Your next ride, arranged in minutes.</h1>
            <p class="ride-subtext">Complete one simple form. Our dispatch team will assign your vehicle and driver, then coordinate the trip schedule.</p>
            <div class="row g-3 mt-2">
                <div class="col-4"><div class="ride-step-card"><span class="ride-step-num">01</span><p>Enter trip details</p></div></div>
                <div class="col-4"><div class="ride-step-card"><span class="ride-step-num">02</span><p>Dispatch assigns driver</p></div></div>
                <div class="col-4"><div class="ride-step-card"><span class="ride-step-num">03</span><p>Trip is scheduled</p></div></div>
            </div>
        </div>
        <div class="col-lg-7">
            <div class="ride-card">
                <div class="d-flex justify-content-between align-items-start mb-4">
                    <div>
                        <p class="ride-eyebrow mb-1">Book a ride</p>
                        <h2 class="fw-bold mb-0">Trip details</h2>
                    </div>
                    <span class="ride-pill">Available daily</span>
                </div>

                <form asp-controller="Booking" asp-action="Create" method="post">
                    @Html.AntiForgeryToken()
                    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

                    <p class="ride-section-label">Your details</p>
                    <div class="row g-3 mb-3">
                        <div class="col-md-6">
                            <label asp-for="CustomerName" class="form-label fw-semibold"></label>
                            <input asp-for="CustomerName" class="form-control ride-input" placeholder="Full name" />
                            <span asp-validation-for="CustomerName" class="text-danger small"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="CustomerPhone" class="form-label fw-semibold"></label>
                            <input asp-for="CustomerPhone" class="form-control ride-input" placeholder="e.g. 0123456789" />
                            <span asp-validation-for="CustomerPhone" class="text-danger small"></span>
                        </div>
                        <div class="col-12">
                            <label asp-for="CustomerEmail" class="form-label fw-semibold"></label>
                            <input asp-for="CustomerEmail" class="form-control ride-input" placeholder="you@example.com" />
                            <span asp-validation-for="CustomerEmail" class="text-danger small"></span>
                        </div>
                    </div>

                    <p class="ride-section-label">Trip</p>
                    <div class="row g-3 mb-3">
                        <div class="col-md-6">
                            <label asp-for="PickupLocation" class="form-label fw-semibold"></label>
                            <input asp-for="PickupLocation" class="form-control ride-input" placeholder="e.g. KL Sentral" />
                            <span asp-validation-for="PickupLocation" class="text-danger small"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="Destination" class="form-label fw-semibold"></label>
                            <input asp-for="Destination" class="form-control ride-input" placeholder="e.g. KLIA Terminal 1" />
                            <span asp-validation-for="Destination" class="text-danger small"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="PickupDate" class="form-label fw-semibold"></label>
                            <input asp-for="PickupDate" type="date" min="@today" max="@maxDate" class="form-control ride-input" />
                            <span asp-validation-for="PickupDate" class="text-danger small"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="PickupTime" class="form-label fw-semibold"></label>
                            <input asp-for="PickupTime" type="time" min="06:00" max="23:45" step="900" class="form-control ride-input" />
                            <span asp-validation-for="PickupTime" class="text-danger small"></span>
                            <div class="form-text">Bookings available 6:00 AM - midnight, 15-minute slots.</div>
                        </div>
                    </div>

                    <p class="ride-section-label">Passengers &amp; vehicle</p>
                    <div class="row g-3 mb-3">
                        <div class="col-md-4">
                            <label asp-for="Passengers" class="form-label fw-semibold"></label>
                            <input asp-for="Passengers" type="number" min="1" max="8" class="form-control ride-input" />
                            <span asp-validation-for="Passengers" class="text-danger small"></span>
                        </div>
                        <div class="col-md-4">
                            <label asp-for="Bags" class="form-label fw-semibold"></label>
                            <input asp-for="Bags" type="number" min="0" max="10" class="form-control ride-input" />
                            <span asp-validation-for="Bags" class="text-danger small"></span>
                        </div>
                        <div class="col-md-4">
                            <label asp-for="VehicleType" class="form-label fw-semibold"></label>
                            <select asp-for="VehicleType" class="form-select ride-input">
                                <option value="">Select...</option>
                                <option value="Car">Car</option>
                                <option value="Van">Van</option>
                                <option value="Bus">Bus</option>
                            </select>
                            <span asp-validation-for="VehicleType" class="text-danger small"></span>
                        </div>
                    </div>

                    <div class="mb-3">
                        <label asp-for="Notes" class="form-label fw-semibold"></label>
                        <textarea asp-for="Notes" rows="3" class="form-control ride-input" placeholder="Flight number, luggage, child seat, or other requests"></textarea>
                        <span asp-validation-for="Notes" class="text-danger small"></span>
                    </div>

                    <p class="ride-section-label">Payment &amp; confirmation</p>
                    <div class="mb-3">
                        <label asp-for="PaymentMethod" class="form-label fw-semibold"></label>
                        <select asp-for="PaymentMethod" class="form-select ride-input">
                            <option value="Pay_at_Pickup">Pay at Pickup</option>
                            <option value="Bank_Transfer">Bank Transfer</option>
                        </select>
                        <span asp-validation-for="PaymentMethod" class="text-danger small"></span>
                    </div>

                    <div class="form-check mb-4">
                        <input asp-for="AcceptedTerms" class="form-check-input" />
                        <label asp-for="AcceptedTerms" class="form-check-label">I accept the terms and conditions</label>
                        <span asp-validation-for="AcceptedTerms" class="text-danger small d-block"></span>
                    </div>

                    <button type="submit" class="btn ride-btn-primary w-100 py-3 fw-bold">Submit booking</button>
                    <p class="text-center text-muted small mt-3 mb-0">No online payment required. Our team will contact you to confirm.</p>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

- [ ] **Step 12: Create the confirmation view**

```html
@* Views/Booking/Confirmation.cshtml *@
@{
    ViewData["Title"] = "Booking Received";
    var reference = ViewBag.BookingReference as string;
}

<div class="ride-card mx-auto" style="max-width: 560px;">
    <p class="ride-eyebrow">Booking received</p>
    <h1 class="fw-bold">Thank you. Your reference is @reference.</h1>
    <p class="ride-subtext">Our dispatch team will review your trip and assign a vehicle and driver. You'll be contacted using the phone number or email you provided.</p>
    <a asp-controller="Booking" asp-action="Create" class="btn ride-btn-primary mt-3">Book another ride</a>
</div>
```

- [ ] **Step 13: Add RideReady-inspired styling**

```css
/* wwwroot/css/site.css - append */
:root {
    --ride-dark-green: #173f2b;
    --ride-mid-green: #20653d;
    --ride-bright-green: #2b7a4b;
    --ride-vivid-green: #1f9d55;
    --ride-bg-1: #f4f7f3;
    --ride-bg-2: #eef3ef;
    --ride-bg-3: #e8f4eb;
    --ride-bg-4: #dff3e5;
    --ride-border: #dce5de;
    --ride-text: #132219;
    --ride-text-muted: #5b6960;
}

body {
    background-color: var(--ride-bg-1);
    color: var(--ride-text);
}

.ride-hero {
    padding: 2rem 0;
}

.ride-eyebrow {
    display: inline-flex;
    background: var(--ride-bg-4);
    color: var(--ride-mid-green);
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.14em;
    padding: 0.5rem 1rem;
    border-radius: 999px;
}

.ride-heading {
    font-weight: 700;
    letter-spacing: -0.03em;
    line-height: 1.1;
}

.ride-subtext {
    color: var(--ride-text-muted);
}

.ride-step-card {
    border: 1px solid var(--ride-border);
    background: rgba(255, 255, 255, 0.7);
    border-radius: 16px;
    padding: 1rem;
}

.ride-step-num {
    color: var(--ride-bright-green);
    font-weight: 700;
    font-size: 0.75rem;
}

.ride-card {
    background: #fff;
    border: 1px solid var(--ride-border);
    border-radius: 28px;
    box-shadow: 0 24px 70px rgba(30, 65, 43, 0.12);
    padding: 1.75rem;
}

.ride-pill {
    background: var(--ride-bg-2);
    border-radius: 999px;
    padding: 0.5rem 0.9rem;
    font-size: 0.75rem;
    font-weight: 600;
}

.ride-section-label {
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.14em;
    color: var(--ride-mid-green);
    margin-top: 1.25rem;
    margin-bottom: 0.5rem;
}

.ride-input {
    border-radius: 12px;
    border-color: var(--ride-border);
    padding: 0.6rem 0.9rem;
}

.ride-input:focus {
    border-color: var(--ride-bright-green);
    box-shadow: 0 0 0 4px rgba(43, 122, 75, 0.15);
}

.ride-btn-primary {
    background-color: var(--ride-dark-green);
    color: #fff;
    border-radius: 12px;
    border: none;
}

.ride-btn-primary:hover {
    background-color: var(--ride-mid-green);
    color: #fff;
}
```

- [ ] **Step 14: Add a "Book a Ride" nav link**

In `Views/Shared/_Layout.cshtml`, add a link inside the `<ul class="navbar-nav flex-grow-1">` list, before the existing Home link's closing `</ul>`:

```html
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-area="" asp-controller="Booking" asp-action="Create">Book a Ride</a>
                        </li>
```

- [ ] **Step 15: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests, including Task 3's)

- [ ] **Step 16: Commit**

```bash
git add ViewModels/ Services/BookingService.cs Tests/ Controllers/BookingController.cs Views/Booking/ wwwroot/css/site.css Views/Shared/_Layout.cshtml
git commit -m "feat: add customer booking form (no fare shown, admin-only pricing)"
```

---

## Part 3: Pricing & Location Services

### Task 5: Google Maps Location Service

**Files:**
- Create: `Services/GoogleMapsSettings.cs`
- Create: `Services/GoogleMapsLocationService.cs`
- Create: `Tests/Services/GoogleMapsLocationServiceTests.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: Google Directions API (`https://maps.googleapis.com/maps/api/directions/json`) — **billable**, per-SKU free tier (see spec §8, cost noted separately); requires a Google Cloud billing account
- Produces: `ILocationService` implementation backed by real Google Maps data, replacing `MockLocationService` in production

> **Cost note:** `IBookingService.GetQuoteAsync` calls both `GetDistanceAsync` and `GetDurationAsync` for the same pickup/destination pair back-to-back. Without caching this would double the number of billable Directions API calls per quote. This service caches the parsed route in memory for 10 minutes so the second call reuses the first call's result.

- [ ] **Step 1: Create the settings class**

```csharp
// Services/GoogleMapsSettings.cs
namespace RideBooking.Services
{
    public class GoogleMapsSettings
    {
        public string ApiKey { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Write a failing test for parsing a Google Directions API response**

```csharp
// Tests/Services/GoogleMapsLocationServiceTests.cs
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson)
            };
            return Task.FromResult(response);
        }
    }

    public class GoogleMapsLocationServiceTests
    {
        private const string SampleDirectionsResponse = @"{
            ""status"": ""OK"",
            ""routes"": [{
                ""legs"": [{
                    ""distance"": { ""value"": 215000, ""text"": ""215 km"" },
                    ""duration"": { ""value"": 9000, ""text"": ""2 hours 30 mins"" }
                }]
            }]
        }";

        [Fact]
        public async Task GetDistanceAsync_WithValidResponse_ReturnsDistanceInKm()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act
            var distance = await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(215m, distance);
        }

        [Fact]
        public async Task GetDurationAsync_WithValidResponse_ReturnsDurationInHours()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act
            var duration = await service.GetDurationAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(2.5m, duration);
        }

        [Fact]
        public async Task GetDistanceThenDuration_ForSameRoute_OnlyCallsApiOnce()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act
            await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");
            await service.GetDurationAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task GetDistanceAsync_WithNonOkStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var errorResponse = @"{ ""status"": ""ZERO_RESULTS"", ""routes"": [] }";
            var handler = new FakeHttpMessageHandler(errorResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetDistanceAsync("Nowhere", "Nowhere Else"));
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~GoogleMapsLocationServiceTests`
Expected: FAIL (`GoogleMapsLocationService` does not exist)

- [ ] **Step 4: Implement GoogleMapsLocationService**

```csharp
// Services/GoogleMapsLocationService.cs
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RideBooking.Services
{
    public class GoogleMapsLocationService : ILocationService
    {
        private readonly HttpClient _httpClient;
        private readonly GoogleMapsSettings _settings;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        public GoogleMapsLocationService(HttpClient httpClient, IOptions<GoogleMapsSettings> settings, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _cache = cache;
        }

        public async Task<decimal> GetDistanceAsync(string pickup, string destination)
        {
            var route = await GetRouteAsync(pickup, destination);
            return route.DistanceKm;
        }

        public async Task<decimal> GetDurationAsync(string pickup, string destination)
        {
            var route = await GetRouteAsync(pickup, destination);
            return route.DurationHours;
        }

        private async Task<(decimal DistanceKm, decimal DurationHours)> GetRouteAsync(string pickup, string destination)
        {
            var cacheKey = $"route:{pickup.Trim().ToLowerInvariant()}|{destination.Trim().ToLowerInvariant()}";

            if (_cache.TryGetValue(cacheKey, out (decimal DistanceKm, decimal DurationHours) cached))
            {
                return cached;
            }

            var url = "https://maps.googleapis.com/maps/api/directions/json" +
                $"?origin={Uri.EscapeDataString(pickup)}" +
                $"&destination={Uri.EscapeDataString(destination)}" +
                $"&key={_settings.ApiKey}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var route = ParseRoute(json);

            _cache.Set(cacheKey, route, CacheDuration);
            return route;
        }

        internal static (decimal DistanceKm, decimal DurationHours) ParseRoute(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString();

            if (status != "OK")
            {
                throw new InvalidOperationException($"Google Directions API returned status: {status}");
            }

            var leg = root.GetProperty("routes")[0].GetProperty("legs")[0];
            var distanceMeters = leg.GetProperty("distance").GetProperty("value").GetInt64();
            var durationSeconds = leg.GetProperty("duration").GetProperty("value").GetInt64();

            return (distanceMeters / 1000m, durationSeconds / 3600m);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~GoogleMapsLocationServiceTests`
Expected: PASS (4 tests)

- [ ] **Step 6: Wire up memory caching and swap the registered ILocationService**

In `Program.cs`, add memory caching, bind the settings, register the typed `HttpClient`, and replace the `MockLocationService` registration:

```csharp
// Before:
// Register booking services
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ILocationService, MockLocationService>();

// After:
builder.Services.AddMemoryCache();
builder.Services.Configure<GoogleMapsSettings>(builder.Configuration.GetSection("GoogleMapsSettings"));
builder.Services.AddHttpClient<GoogleMapsLocationService>();

// Register booking services
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ILocationService>(sp => sp.GetRequiredService<GoogleMapsLocationService>());
```

- [ ] **Step 7: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 8: Commit**

```bash
git add Services/GoogleMapsSettings.cs Services/GoogleMapsLocationService.cs Tests/Services/GoogleMapsLocationServiceTests.cs Program.cs
git commit -m "feat: replace mock location service with Google Maps Directions API"
```

---

## Part 4: Admin Dashboard

### Task 6: Admin Dashboard (Booking List, Driver Assignment, Status Updates)

**Files:**
- Create: `Services/PasswordHasher.cs`
- Create: `Services/IDriverAssignmentService.cs`
- Create: `Services/DriverAssignmentService.cs`
- Create: `Tests/Services/DriverAssignmentServiceTests.cs`
- Create: `ViewModels/AdminBookingListItemViewModel.cs`
- Create: `ViewModels/AssignDriverViewModel.cs`
- Create: `ViewModels/CreateDriverViewModel.cs`
- Create: `ViewModels/AdminLoginViewModel.cs`
- Create: `Services/AdminCredentialsSettings.cs`
- Create: `Controllers/AdminAuthController.cs`
- Create: `Controllers/AdminController.cs`
- Create: `Tests/Controllers/AdminControllerTests.cs`
- Create: `Views/AdminAuth/Login.cshtml`
- Create: `Views/Admin/Index.cshtml`
- Modify: `Models/Driver.cs`
- Modify: `Program.cs`
- Modify: `appsettings.json`

**Interfaces:**
- Consumes: `RideBookingDbContext` (Bookings, Drivers, DriverAssignments, BookingStatusHistory)
- Produces: `IDriverAssignmentService`, `GET/POST /Admin/*` (cookie-protected), `GET/POST /AdminAuth/Login`

> **Scope for "basic" admin dashboard (per roadmap):** a protected booking queue with fare visibility, driver assignment from a roster, and manual status updates. Live map, full analytics/reporting, and a dedicated driver CRUD screen (§3.2) are deferred — drivers are added inline from the assignment form instead of a separate screen.

- [ ] **Step 1: Add a PIN field to the Driver roster (used for Driver Portal login in Task 7)**

```csharp
// Models/Driver.cs
namespace RideBooking.Models
{
    public class Driver
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty; // Car, Van, Bus
        public string? VehicleNumber { get; set; }
        public string PinHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public decimal? Rating { get; set; }
        public decimal CancellationRate { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DriverAssignment> Assignments { get; set; } = new List<DriverAssignment>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
    }
}
```

- [ ] **Step 2: Create the migration for the new field**

Run: `dotnet ef migrations add AddDriverPinHash`
Run: `dotnet ef database update`

- [ ] **Step 3: Create the PBKDF2 password hasher (no extra NuGet package needed)**

```csharp
// Services/PasswordHasher.cs
using System.Security.Cryptography;

namespace RideBooking.Services
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static string Hash(string plainText)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string plainText, string hashed)
        {
            var parts = hashed.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
```

- [ ] **Step 4: Write a failing test for the password hasher**

```csharp
// Tests/Services/PasswordHasherTests.cs
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ThenVerify_WithCorrectPlainText_ReturnsTrue()
        {
            var hash = PasswordHasher.Hash("1234");
            Assert.True(PasswordHasher.Verify("1234", hash));
        }

        [Fact]
        public void Hash_ThenVerify_WithWrongPlainText_ReturnsFalse()
        {
            var hash = PasswordHasher.Hash("1234");
            Assert.False(PasswordHasher.Verify("9999", hash));
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~PasswordHasherTests`
Expected: PASS (2 tests) — `PasswordHasher` is a static utility with no external dependencies, so this confirms correctness directly rather than needing a red step.

- [ ] **Step 5: Create ViewModels**

```csharp
// ViewModels/AdminBookingListItemViewModel.cs
namespace RideBooking.ViewModels
{
    public class AdminBookingListItemViewModel
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string RequestedVehicleType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? EstimatedFare { get; set; }
        public int? AssignedDriverId { get; set; }
        public string? AssignedDriverName { get; set; }
        public string? AssignedDriverPhone { get; set; }
        public string? AssignmentStatus { get; set; }
    }
}
```

```csharp
// ViewModels/AssignDriverViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class AssignDriverViewModel
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public int DriverId { get; set; }
    }
}
```

```csharp
// ViewModels/CreateDriverViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class CreateDriverViewModel
    {
        [Required(ErrorMessage = "Driver name is required")]
        [StringLength(255, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$",
            ErrorMessage = "Invalid Malaysian phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle type is required")]
        [RegularExpression("^(Car|Van|Bus)$")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle number is required")]
        [StringLength(50)]
        public string VehicleNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "A 4-6 digit PIN is required for driver portal login")]
        [RegularExpression(@"^\d{4,6}$", ErrorMessage = "PIN must be 4-6 digits")]
        public string Pin { get; set; } = string.Empty;
    }
}
```

```csharp
// ViewModels/AdminLoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class AdminLoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
```

```csharp
// Services/AdminCredentialsSettings.cs
namespace RideBooking.Services
{
    public class AdminCredentialsSettings
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 6: Write a failing test for the driver assignment service**

```csharp
// Tests/Services/DriverAssignmentServiceTests.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class DriverAssignmentServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideBookingDbContext context)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0001",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        [Fact]
        public async Task CreateDriverAsync_WithValidRequest_HashesThePin()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var request = new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            };

            // Act
            var driver = await service.CreateDriverAsync(request);

            // Assert
            Assert.NotEqual("1234", driver.PinHash);
            Assert.True(PasswordHasher.Verify("1234", driver.PinHash));
        }

        [Fact]
        public async Task AssignDriverAsync_WithNewAssignment_SetsBookingStatusToDriverAssigned()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
            var assignment = await context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == booking.Id && a.DriverId == driver.Id);
            Assert.NotNull(assignment);
            Assert.Equal("Pending", assignment!.AssignmentStatus);
        }

        [Fact]
        public async Task AssignDriverAsync_CalledTwiceForSameDriver_DoesNotDuplicateAssignment()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });

            // Act
            await service.AssignDriverAsync(booking.Id, driver.Id);
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Assert
            var count = await context.DriverAssignments
                .CountAsync(a => a.BookingId == booking.Id && a.DriverId == driver.Id);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task GetDashboardBookingsAsync_ReturnsBookingsWithAssignmentInfo()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);
            var driver = await service.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            });
            await service.AssignDriverAsync(booking.Id, driver.Id);

            // Act
            var result = await service.GetDashboardBookingsAsync();

            // Assert
            var item = Assert.Single(result);
            Assert.Equal("Ah Seng", item.AssignedDriverName);
            Assert.Equal("Driver_Assigned", item.Status);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_WritesStatusHistory()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var booking = await SeedBookingAsync(context);

            // Act
            await service.UpdateBookingStatusAsync(booking.Id, "Confirmed", "Admin");

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Confirmed", updated!.Status);
            var history = await context.BookingStatusHistories.FirstOrDefaultAsync(h => h.BookingId == booking.Id);
            Assert.NotNull(history);
            Assert.Equal("New", history!.PreviousStatus);
            Assert.Equal("Confirmed", history.NewStatus);
            Assert.Equal("Admin", history.ChangedBy);
        }
    }
}
```

- [ ] **Step 7: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~DriverAssignmentServiceTests`
Expected: FAIL (`DriverAssignmentService` does not exist)

- [ ] **Step 8: Implement IDriverAssignmentService and DriverAssignmentService**

```csharp
// Services/IDriverAssignmentService.cs
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IDriverAssignmentService
    {
        Task<List<AdminBookingListItemViewModel>> GetDashboardBookingsAsync();
        Task<List<Driver>> GetActiveDriversAsync();
        Task<Driver> CreateDriverAsync(CreateDriverViewModel request);
        Task AssignDriverAsync(int bookingId, int driverId);
        Task UpdateBookingStatusAsync(int bookingId, string newStatus, string changedBy);
    }
}
```

```csharp
// Services/DriverAssignmentService.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public class DriverAssignmentService : IDriverAssignmentService
    {
        private static readonly string[] ValidStatuses =
        {
            "New", "Confirmed", "Driver_Assigned", "Picked_Up", "In_Transit",
            "Dropped_Off", "Completed", "Cancelled", "No_Show"
        };

        private readonly RideBookingDbContext _context;

        public DriverAssignmentService(RideBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<AdminBookingListItemViewModel>> GetDashboardBookingsAsync()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Quote)
                .Where(b => b.Status != "Completed" && b.Status != "Cancelled")
                .OrderBy(b => b.PickupDate)
                .ThenBy(b => b.PickupTime)
                .ToListAsync();

            var bookingIds = bookings.Select(b => b.Id).ToList();

            var allAssignments = await _context.DriverAssignments
                .Include(a => a.Driver)
                .Where(a => bookingIds.Contains(a.BookingId))
                .ToListAsync();

            var latestByBooking = allAssignments
                .GroupBy(a => a.BookingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AssignedAt).First());

            return bookings.Select(b =>
            {
                latestByBooking.TryGetValue(b.Id, out var assignment);
                return new AdminBookingListItemViewModel
                {
                    BookingId = b.Id,
                    BookingReference = b.BookingReference,
                    CustomerName = b.Customer?.Name ?? string.Empty,
                    CustomerPhone = b.Customer?.Phone ?? string.Empty,
                    PickupLocation = b.PickupLocation,
                    Destination = b.Destination,
                    PickupDate = b.PickupDate,
                    PickupTime = b.PickupTime,
                    Passengers = b.Passengers,
                    Bags = b.Bags,
                    RequestedVehicleType = b.RequestedVehicleType,
                    Status = b.Status,
                    EstimatedFare = b.Quote?.TotalEstimatedFare,
                    AssignedDriverId = assignment?.DriverId,
                    AssignedDriverName = assignment?.Driver?.Name,
                    AssignedDriverPhone = assignment?.Driver?.Phone,
                    AssignmentStatus = assignment?.AssignmentStatus
                };
            }).ToList();
        }

        public async Task<List<Driver>> GetActiveDriversAsync()
        {
            return await _context.Drivers
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Driver> CreateDriverAsync(CreateDriverViewModel request)
        {
            var driver = new Driver
            {
                Name = request.Name,
                Phone = request.Phone,
                VehicleType = request.VehicleType,
                VehicleNumber = request.VehicleNumber,
                PinHash = PasswordHasher.Hash(request.Pin)
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();
            return driver;
        }

        public async Task AssignDriverAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var existing = await _context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == bookingId && a.DriverId == driverId);

            if (existing == null)
            {
                _context.DriverAssignments.Add(new DriverAssignment
                {
                    BookingId = bookingId,
                    DriverId = driverId,
                    AssignedAt = DateTime.UtcNow,
                    AssignmentStatus = "Pending"
                });
            }
            else
            {
                existing.AssignedAt = DateTime.UtcNow;
                existing.AssignmentStatus = "Pending";
                existing.AcceptedAt = null;
                existing.RejectedAt = null;
            }

            var previousStatus = booking.Status;
            booking.Status = "Driver_Assigned";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = "Driver_Assigned",
                ChangedBy = "Admin"
            });

            await _context.SaveChangesAsync();
        }

        public async Task UpdateBookingStatusAsync(int bookingId, string newStatus, string changedBy)
        {
            if (!ValidStatuses.Contains(newStatus))
            {
                throw new InvalidOperationException($"'{newStatus}' is not a valid booking status");
            }

            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var previousStatus = booking.Status;
            booking.Status = newStatus;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                ChangedBy = changedBy
            });

            await _context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~DriverAssignmentServiceTests`
Expected: PASS (5 tests)

- [ ] **Step 10: Register the AdminAuth cookie scheme and bind admin credentials**

In `appsettings.json`, add a new top-level section (placeholder credential — override via an environment variable or gitignored `appsettings.Production.json` before deploying):

```json
  "AdminCredentials": {
    "Username": "admin",
    "Password": "ChangeMe123!"
  }
```

In `Program.cs`, add after the `AddDbContext` block:

```csharp
builder.Services.Configure<AdminCredentialsSettings>(builder.Configuration.GetSection("AdminCredentials"));

builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "RideBooking.AdminAuth";
        options.LoginPath = "/AdminAuth/Login";
        options.AccessDeniedPath = "/AdminAuth/Login";
    });
builder.Services.AddAuthorization();
```

And add `app.UseAuthentication();` immediately before the existing `app.UseAuthorization();` line:

```csharp
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
```

Also register the new service alongside the existing ones:

```csharp
builder.Services.AddScoped<IDriverAssignmentService, DriverAssignmentService>();
```

- [ ] **Step 11: Implement AdminAuthController**

```csharp
// Controllers/AdminAuthController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    public class AdminAuthController : Controller
    {
        private readonly AdminCredentialsSettings _credentials;

        public AdminAuthController(IOptions<AdminCredentialsSettings> credentials)
        {
            _credentials = credentials.Value;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Username != _credentials.Username || model.Password != _credentials.Password)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View(model);
            }

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, model.Username) },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync("AdminAuth", new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminAuth");
            return RedirectToAction(nameof(Login));
        }
    }
}
```

- [ ] **Step 12: Implement AdminController**

```csharp
// Controllers/AdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AdminController : Controller
    {
        private readonly IDriverAssignmentService _driverAssignmentService;

        public AdminController(IDriverAssignmentService driverAssignmentService)
        {
            _driverAssignmentService = driverAssignmentService;
        }

        public async Task<IActionResult> Index()
        {
            var bookings = await _driverAssignmentService.GetDashboardBookingsAsync();
            ViewBag.ActiveDrivers = await _driverAssignmentService.GetActiveDriversAsync();
            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDriver(AssignDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.AssignDriverAsync(model.BookingId, model.DriverId);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriver(CreateDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.CreateDriverAsync(model);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            await _driverAssignmentService.UpdateBookingStatusAsync(bookingId, newStatus, User.Identity?.Name ?? "Admin");
            return RedirectToAction(nameof(Index));
        }
    }
}
```

- [ ] **Step 13: Write and run controller tests**

```csharp
// Tests/Controllers/AdminControllerTests.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Controllers
{
    public class AdminControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        [Fact]
        public async Task Index_ReturnsViewWithBookingList()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            Assert.IsAssignableFrom<List<AdminBookingListItemViewModel>>(view.Model);
        }

        [Fact]
        public async Task CreateDriver_WithValidModel_RedirectsToIndex()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new DriverAssignmentService(context);
            var controller = new AdminController(service);
            var model = new CreateDriverViewModel
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                Pin = "1234"
            };

            // Act
            var result = await controller.CreateDriver(model);

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await context.Drivers.CountAsync());
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~AdminControllerTests`
Expected: PASS (2 tests)

- [ ] **Step 14: Create the login view**

```html
@* Views/AdminAuth/Login.cshtml *@
@model RideBooking.ViewModels.AdminLoginViewModel
@{
    ViewData["Title"] = "Admin Login";
}

<div class="ride-card mx-auto" style="max-width: 420px;">
    <p class="ride-eyebrow">Dispatch back office</p>
    <h1 class="fw-bold h3">Admin login</h1>
    <form asp-controller="AdminAuth" asp-action="Login" method="post" class="mt-3">
        @Html.AntiForgeryToken()
        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
        <div class="mb-3">
            <label asp-for="Username" class="form-label fw-semibold"></label>
            <input asp-for="Username" class="form-control ride-input" />
        </div>
        <div class="mb-3">
            <label asp-for="Password" class="form-label fw-semibold"></label>
            <input asp-for="Password" type="password" class="form-control ride-input" />
        </div>
        <button type="submit" class="btn ride-btn-primary w-100 fw-bold">Sign in</button>
    </form>
</div>
```

- [ ] **Step 15: Create the admin dashboard view**

```html
@* Views/Admin/Index.cshtml *@
@model List<RideBooking.ViewModels.AdminBookingListItemViewModel>
@{
    ViewData["Title"] = "Dispatch Back Office";
    var drivers = ViewBag.ActiveDrivers as List<RideBooking.Models.Driver> ?? new();
    var statuses = new[] { "New", "Confirmed", "Driver_Assigned", "Picked_Up", "In_Transit", "Dropped_Off", "Completed", "Cancelled", "No_Show" };
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <p class="ride-eyebrow mb-1">RideBooking</p>
        <h1 class="fw-bold">Dispatch back office</h1>
    </div>
    <form asp-controller="AdminAuth" asp-action="Logout" method="post">
        @Html.AntiForgeryToken()
        <button type="submit" class="btn btn-outline-secondary rounded-pill">Sign out</button>
    </form>
</div>

<div class="ride-card mb-4">
    <h2 class="h5 fw-bold mb-3">Add a driver</h2>
    <form asp-controller="Admin" asp-action="CreateDriver" method="post" class="row g-2 align-items-end">
        @Html.AntiForgeryToken()
        <div class="col-md-2"><input name="Name" class="form-control ride-input" placeholder="Driver name" required /></div>
        <div class="col-md-2"><input name="Phone" class="form-control ride-input" placeholder="Phone" required /></div>
        <div class="col-md-2">
            <select name="VehicleType" class="form-select ride-input" required>
                <option value="Car">Car</option>
                <option value="Van">Van</option>
                <option value="Bus">Bus</option>
            </select>
        </div>
        <div class="col-md-2"><input name="VehicleNumber" class="form-control ride-input" placeholder="Plate no." required /></div>
        <div class="col-md-2"><input name="Pin" class="form-control ride-input" placeholder="4-6 digit PIN" required /></div>
        <div class="col-md-2"><button type="submit" class="btn ride-btn-primary w-100">Add driver</button></div>
    </form>
</div>

@foreach (var booking in Model)
{
    <div class="ride-card mb-3">
        <div class="d-flex justify-content-between flex-wrap gap-2">
            <div>
                <div class="d-flex align-items-center gap-2">
                    <h2 class="h5 fw-bold mb-0">@booking.CustomerName</h2>
                    <span class="ride-pill">@booking.Status</span>
                </div>
                <p class="text-muted small mb-0">@booking.BookingReference &middot; @booking.CustomerPhone</p>
            </div>
            <div class="text-end">
                <p class="fw-bold mb-0">@booking.PickupDate.ToString("yyyy-MM-dd") &middot; @booking.PickupTime.ToString("HH:mm")</p>
                <p class="text-muted small mb-0">Est. fare: RM @(booking.EstimatedFare?.ToString("0.00") ?? "-")</p>
            </div>
        </div>

        <div class="ride-section-label">Trip</div>
        <p class="mb-1">@booking.PickupLocation &rarr; @booking.Destination</p>
        <p class="text-muted small">@booking.Passengers pax &middot; @booking.Bags bags &middot; @booking.RequestedVehicleType</p>

        <div class="row g-2 mt-2">
            <div class="col-md-6">
                <form asp-controller="Admin" asp-action="AssignDriver" method="post" class="d-flex gap-2">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="BookingId" value="@booking.BookingId" />
                    <select name="DriverId" class="form-select ride-input">
                        @foreach (var driver in drivers)
                        {
                            @if (driver.Id == booking.AssignedDriverId)
                            {
                                <option value="@driver.Id" selected>@driver.Name (@driver.VehicleType)</option>
                            }
                            else
                            {
                                <option value="@driver.Id">@driver.Name (@driver.VehicleType)</option>
                            }
                        }
                    </select>
                    <button type="submit" class="btn ride-btn-primary text-nowrap">Assign</button>
                </form>
            </div>
            <div class="col-md-6">
                <form asp-controller="Admin" asp-action="UpdateStatus" method="post" class="d-flex gap-2">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="bookingId" value="@booking.BookingId" />
                    <select name="newStatus" class="form-select ride-input">
                        @foreach (var status in statuses)
                        {
                            @if (status == booking.Status)
                            {
                                <option value="@status" selected>@status</option>
                            }
                            else
                            {
                                <option value="@status">@status</option>
                            }
                        }
                    </select>
                    <button type="submit" class="btn btn-outline-secondary text-nowrap">Update status</button>
                </form>
            </div>
        </div>

        @if (booking.AssignedDriverName != null)
        {
            <p class="text-muted small mt-2 mb-0">Assigned: @booking.AssignedDriverName (@booking.AssignedDriverPhone) &middot; @booking.AssignmentStatus</p>
        }
    </div>
}
```

- [ ] **Step 16: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 17: Commit**

```bash
git add Models/Driver.cs Migrations/ Services/ ViewModels/ Controllers/AdminAuthController.cs Controllers/AdminController.cs Tests/ Views/AdminAuth/ Views/Admin/ Program.cs appsettings.json
git commit -m "feat: add admin dashboard with driver assignment and status updates"
```

---

## Part 5: Driver Portal

### Task 7: Driver Portal (Accept/Reject, Status Updates, Browser-Based Location)

**Files:**
- Create: `ViewModels/DriverLoginViewModel.cs`
- Create: `ViewModels/DriverAssignmentListItemViewModel.cs`
- Create: `ViewModels/LocationReportViewModel.cs`
- Create: `Services/IDriverPortalService.cs`
- Create: `Services/DriverPortalService.cs`
- Create: `Tests/Services/DriverPortalServiceTests.cs`
- Create: `Controllers/DriverAuthController.cs`
- Create: `Controllers/DriverController.cs`
- Create: `Tests/Controllers/DriverControllerTests.cs`
- Create: `Views/DriverAuth/Login.cshtml`
- Create: `Views/Driver/Index.cshtml`
- Create: `wwwroot/js/driver-location.js`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: `RideBookingDbContext` (DriverAssignments, Bookings, DriverLocations), `PasswordHasher`
- Produces: `IDriverPortalService`, `GET/POST /Driver/*` (cookie-protected), `GET/POST /DriverAuth/Login`, `POST /Driver/ReportLocation` (JSON, browser geolocation)

> Per spec §8.2, location reporting is browser-based only (foreground, best-effort) — no native app or hardware tracker.

- [ ] **Step 1: Create ViewModels**

```csharp
// ViewModels/DriverLoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace RideBooking.ViewModels
{
    public class DriverLoginViewModel
    {
        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Pin { get; set; } = string.Empty;
    }
}
```

```csharp
// ViewModels/DriverAssignmentListItemViewModel.cs
namespace RideBooking.ViewModels
{
    public class DriverAssignmentListItemViewModel
    {
        public int AssignmentId { get; set; }
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PickupLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly PickupDate { get; set; }
        public TimeOnly PickupTime { get; set; }
        public int Passengers { get; set; }
        public int Bags { get; set; }
        public string? Notes { get; set; }
        public string AssignmentStatus { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
    }
}
```

```csharp
// ViewModels/LocationReportViewModel.cs
namespace RideBooking.ViewModels
{
    public class LocationReportViewModel
    {
        public int? BookingId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int? AccuracyMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
    }
}
```

- [ ] **Step 2: Write a failing test for the driver portal service**

```csharp
// Tests/Services/DriverPortalServiceTests.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class DriverPortalServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<(Driver Driver, Booking Booking, DriverAssignment Assignment)> SeedAssignedBookingAsync(RideBookingDbContext context)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);

            var driver = new Driver
            {
                Name = "Ah Seng",
                Phone = "0123456789",
                VehicleType = "Car",
                VehicleNumber = "ABC 1234",
                PinHash = PasswordHasher.Hash("1234")
            };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0002",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = "Driver_Assigned"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var assignment = new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = "Pending"
            };
            context.DriverAssignments.Add(assignment);
            await context.SaveChangesAsync();

            return (driver, booking, assignment);
        }

        [Fact]
        public async Task AuthenticateAsync_WithCorrectPin_ReturnsDriver()
        {
            var context = GetInMemoryDbContext();
            var (driver, _, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.AuthenticateAsync(driver.Phone, "1234");

            Assert.NotNull(result);
            Assert.Equal(driver.Id, result!.Id);
        }

        [Fact]
        public async Task AuthenticateAsync_WithWrongPin_ReturnsNull()
        {
            var context = GetInMemoryDbContext();
            var (driver, _, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.AuthenticateAsync(driver.Phone, "0000");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAssignmentsAsync_ReturnsOnlyThatDriversAssignments()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            var result = await service.GetAssignmentsAsync(driver.Id);

            var item = Assert.Single(result);
            Assert.Equal(booking.BookingReference, item.BookingReference);
        }

        [Fact]
        public async Task AcceptAssignmentAsync_SetsAssignmentAcceptedAndBookingConfirmed()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await service.AcceptAssignmentAsync(assignment.Id, driver.Id);

            var updatedAssignment = await context.DriverAssignments.FindAsync(assignment.Id);
            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Accepted", updatedAssignment!.AssignmentStatus);
            Assert.NotNull(updatedAssignment.AcceptedAt);
            Assert.Equal("Confirmed", updatedBooking!.Status);
        }

        [Fact]
        public async Task AcceptAssignmentAsync_ForADifferentDriver_ThrowsInvalidOperationException()
        {
            var context = GetInMemoryDbContext();
            var (_, _, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AcceptAssignmentAsync(assignment.Id, driverId: 9999));
        }

        [Fact]
        public async Task RejectAssignmentAsync_SetsAssignmentRejectedAndBookingBackToNew()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await service.RejectAssignmentAsync(assignment.Id, driver.Id);

            var updatedAssignment = await context.DriverAssignments.FindAsync(assignment.Id);
            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Rejected", updatedAssignment!.AssignmentStatus);
            Assert.Equal("New", updatedBooking!.Status);
        }

        [Fact]
        public async Task UpdateTripStatusAsync_WithAcceptedAssignment_UpdatesBookingStatus()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, assignment) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);
            await service.AcceptAssignmentAsync(assignment.Id, driver.Id);

            await service.UpdateTripStatusAsync(booking.Id, driver.Id, "Picked_Up");

            var updatedBooking = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Picked_Up", updatedBooking!.Status);
        }

        [Fact]
        public async Task RecordLocationAsync_PersistsALocationRow()
        {
            var context = GetInMemoryDbContext();
            var (driver, booking, _) = await SeedAssignedBookingAsync(context);
            var service = new DriverPortalService(context);

            await service.RecordLocationAsync(driver.Id, booking.Id, 3.1390m, 101.6869m, 15, 42.5m);

            var count = await context.DriverLocations.CountAsync(l => l.DriverId == driver.Id);
            Assert.Equal(1, count);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~DriverPortalServiceTests`
Expected: FAIL (`DriverPortalService` does not exist)

- [ ] **Step 4: Implement IDriverPortalService and DriverPortalService**

```csharp
// Services/IDriverPortalService.cs
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public interface IDriverPortalService
    {
        Task<Driver?> AuthenticateAsync(string phone, string pin);
        Task<List<DriverAssignmentListItemViewModel>> GetAssignmentsAsync(int driverId);
        Task AcceptAssignmentAsync(int assignmentId, int driverId);
        Task RejectAssignmentAsync(int assignmentId, int driverId);
        Task UpdateTripStatusAsync(int bookingId, int driverId, string newStatus);
        Task RecordLocationAsync(int driverId, int? bookingId, decimal latitude, decimal longitude, int? accuracyMeters, decimal? speedKmh);
    }
}
```

```csharp
// Services/DriverPortalService.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.ViewModels;

namespace RideBooking.Services
{
    public class DriverPortalService : IDriverPortalService
    {
        private static readonly string[] DriverTripStatuses = { "Picked_Up", "In_Transit", "Dropped_Off", "Completed" };

        private readonly RideBookingDbContext _context;

        public DriverPortalService(RideBookingDbContext context)
        {
            _context = context;
        }

        public async Task<Driver?> AuthenticateAsync(string phone, string pin)
        {
            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Phone == phone && d.IsActive);
            if (driver == null || !PasswordHasher.Verify(pin, driver.PinHash))
            {
                return null;
            }
            return driver;
        }

        public async Task<List<DriverAssignmentListItemViewModel>> GetAssignmentsAsync(int driverId)
        {
            var assignments = await _context.DriverAssignments
                .Include(a => a.Booking)
                    .ThenInclude(b => b!.Customer)
                .Where(a => a.DriverId == driverId && a.AssignmentStatus != "Rejected")
                .ToListAsync();

            return assignments
                .Where(a => a.Booking != null)
                .OrderBy(a => a.Booking!.PickupDate)
                .ThenBy(a => a.Booking!.PickupTime)
                .Select(a => new DriverAssignmentListItemViewModel
                {
                    AssignmentId = a.Id,
                    BookingId = a.Booking!.Id,
                    BookingReference = a.Booking.BookingReference,
                    CustomerName = a.Booking.Customer?.Name ?? string.Empty,
                    CustomerPhone = a.Booking.Customer?.Phone ?? string.Empty,
                    PickupLocation = a.Booking.PickupLocation,
                    Destination = a.Booking.Destination,
                    PickupDate = a.Booking.PickupDate,
                    PickupTime = a.Booking.PickupTime,
                    Passengers = a.Booking.Passengers,
                    Bags = a.Booking.Bags,
                    Notes = a.Booking.Notes,
                    AssignmentStatus = a.AssignmentStatus,
                    BookingStatus = a.Booking.Status
                })
                .ToList();
        }

        public async Task AcceptAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await GetOwnedAssignmentAsync(assignmentId, driverId);
            var booking = assignment.Booking ?? await _context.Bookings.FindAsync(assignment.BookingId)
                ?? throw new InvalidOperationException($"Booking {assignment.BookingId} not found");

            assignment.AssignmentStatus = "Accepted";
            assignment.AcceptedAt = DateTime.UtcNow;

            var previousStatus = booking.Status;
            booking.Status = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = previousStatus,
                NewStatus = "Confirmed",
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task RejectAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await GetOwnedAssignmentAsync(assignmentId, driverId);
            var booking = assignment.Booking ?? await _context.Bookings.FindAsync(assignment.BookingId)
                ?? throw new InvalidOperationException($"Booking {assignment.BookingId} not found");

            assignment.AssignmentStatus = "Rejected";
            assignment.RejectedAt = DateTime.UtcNow;

            var previousStatus = booking.Status;
            booking.Status = "New";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = previousStatus,
                NewStatus = "New",
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task UpdateTripStatusAsync(int bookingId, int driverId, string newStatus)
        {
            if (!DriverTripStatuses.Contains(newStatus))
            {
                throw new InvalidOperationException($"'{newStatus}' is not a status a driver can set");
            }

            var assignment = await _context.DriverAssignments
                .FirstOrDefaultAsync(a => a.BookingId == bookingId && a.DriverId == driverId && a.AssignmentStatus == "Accepted")
                ?? throw new InvalidOperationException("No accepted assignment found for this driver and booking");

            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var previousStatus = booking.Status;
            booking.Status = newStatus;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = bookingId,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
        }

        public async Task RecordLocationAsync(int driverId, int? bookingId, decimal latitude, decimal longitude, int? accuracyMeters, decimal? speedKmh)
        {
            _context.DriverLocations.Add(new DriverLocation
            {
                DriverId = driverId,
                BookingId = bookingId,
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracyMeters,
                SpeedKmh = speedKmh
            });

            await _context.SaveChangesAsync();
        }

        private async Task<DriverAssignment> GetOwnedAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await _context.DriverAssignments
                .Include(a => a.Booking)
                .FirstOrDefaultAsync(a => a.Id == assignmentId)
                ?? throw new InvalidOperationException($"Assignment {assignmentId} not found");

            if (assignment.DriverId != driverId)
            {
                throw new InvalidOperationException("This assignment does not belong to the current driver");
            }

            return assignment;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~DriverPortalServiceTests`
Expected: PASS (8 tests)

- [ ] **Step 6: Add the DriverAuth cookie scheme**

In `Program.cs`, extend the authentication block added in Task 6 by chaining a second cookie scheme:

```csharp
// Before:
builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "RideBooking.AdminAuth";
        options.LoginPath = "/AdminAuth/Login";
        options.AccessDeniedPath = "/AdminAuth/Login";
    });
builder.Services.AddAuthorization();

// After:
builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "RideBooking.AdminAuth";
        options.LoginPath = "/AdminAuth/Login";
        options.AccessDeniedPath = "/AdminAuth/Login";
    })
    .AddCookie("DriverAuth", options =>
    {
        options.Cookie.Name = "RideBooking.DriverAuth";
        options.LoginPath = "/DriverAuth/Login";
        options.AccessDeniedPath = "/DriverAuth/Login";
    });
builder.Services.AddAuthorization();
```

Also register the new service:

```csharp
builder.Services.AddScoped<IDriverPortalService, DriverPortalService>();
```

- [ ] **Step 7: Implement DriverAuthController**

```csharp
// Controllers/DriverAuthController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    public class DriverAuthController : Controller
    {
        private readonly IDriverPortalService _driverPortalService;

        public DriverAuthController(IDriverPortalService driverPortalService)
        {
            _driverPortalService = driverPortalService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(DriverLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var driver = await _driverPortalService.AuthenticateAsync(model.Phone, model.Pin);
            if (driver == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid phone number or PIN");
                return View(model);
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, driver.Id.ToString()),
                    new Claim(ClaimTypes.Name, driver.Name)
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync("DriverAuth", new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Driver");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("DriverAuth");
            return RedirectToAction(nameof(Login));
        }
    }
}
```

- [ ] **Step 8: Implement DriverController**

```csharp
// Controllers/DriverController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RideBooking.Services;
using RideBooking.ViewModels;

namespace RideBooking.Controllers
{
    [Authorize(AuthenticationSchemes = "DriverAuth")]
    public class DriverController : Controller
    {
        private readonly IDriverPortalService _driverPortalService;

        public DriverController(IDriverPortalService driverPortalService)
        {
            _driverPortalService = driverPortalService;
        }

        public async Task<IActionResult> Index()
        {
            var assignments = await _driverPortalService.GetAssignmentsAsync(GetCurrentDriverId());
            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int assignmentId)
        {
            await _driverPortalService.AcceptAssignmentAsync(assignmentId, GetCurrentDriverId());
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int assignmentId)
        {
            await _driverPortalService.RejectAssignmentAsync(assignmentId, GetCurrentDriverId());
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            await _driverPortalService.UpdateTripStatusAsync(bookingId, GetCurrentDriverId(), newStatus);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] // low-risk telemetry write scoped to the authenticated driver's own location
        public async Task<IActionResult> ReportLocation([FromBody] LocationReportViewModel model)
        {
            await _driverPortalService.RecordLocationAsync(
                GetCurrentDriverId(), model.BookingId, model.Latitude, model.Longitude, model.AccuracyMeters, model.SpeedKmh);
            return Ok();
        }

        private int GetCurrentDriverId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
```

- [ ] **Step 9: Write and run controller tests**

```csharp
// Tests/Controllers/DriverControllerTests.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideBooking.Controllers;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using RideBooking.ViewModels;
using Xunit;

namespace RideBooking.Tests.Controllers
{
    public class DriverControllerTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private static DriverController WithAuthenticatedDriver(IDriverPortalService service, int driverId)
        {
            var controller = new DriverController(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, driverId.ToString()) },
                            "TestAuth"))
                    }
                }
            };
            return controller;
        }

        [Fact]
        public async Task Index_ReturnsViewWithOnlyCurrentDriversAssignments()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = PasswordHasher.Hash("1234") };
            var otherDriver = new Driver { Name = "Bob", Phone = "0198765432", VehicleType = "Car", PinHash = PasswordHasher.Hash("5678") };
            context.Drivers.AddRange(driver, otherDriver);

            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0003",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "Driver_Assigned"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            context.DriverAssignments.Add(new DriverAssignment { BookingId = booking.Id, DriverId = driver.Id, AssignmentStatus = "Pending" });
            await context.SaveChangesAsync();

            var service = new DriverPortalService(context);
            var controller = WithAuthenticatedDriver(service, driver.Id);

            // Act
            var result = await controller.Index();

            // Assert
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<DriverAssignmentListItemViewModel>>(view.Model);
            Assert.Single(model);
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~DriverControllerTests`
Expected: PASS (1 test)

- [ ] **Step 10: Create the driver login view**

```html
@* Views/DriverAuth/Login.cshtml *@
@model RideBooking.ViewModels.DriverLoginViewModel
@{
    ViewData["Title"] = "Driver Login";
}

<div class="ride-card mx-auto" style="max-width: 420px;">
    <p class="ride-eyebrow">RideBooking Driver</p>
    <h1 class="fw-bold h3">Driver login</h1>
    <form asp-controller="DriverAuth" asp-action="Login" method="post" class="mt-3">
        @Html.AntiForgeryToken()
        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
        <div class="mb-3">
            <label asp-for="Phone" class="form-label fw-semibold"></label>
            <input asp-for="Phone" class="form-control ride-input" placeholder="e.g. 0123456789" />
        </div>
        <div class="mb-3">
            <label asp-for="Pin" class="form-label fw-semibold"></label>
            <input asp-for="Pin" type="password" inputmode="numeric" class="form-control ride-input" />
        </div>
        <button type="submit" class="btn ride-btn-primary w-100 fw-bold">Sign in</button>
    </form>
</div>
```

- [ ] **Step 11: Create the driver assignments view**

```html
@* Views/Driver/Index.cshtml *@
@model List<RideBooking.ViewModels.DriverAssignmentListItemViewModel>
@{
    ViewData["Title"] = "My Assigned Trips";
    var activeBooking = Model.FirstOrDefault(a => a.AssignmentStatus == "Accepted" && a.BookingStatus != "Completed");
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <p class="ride-eyebrow mb-1">RideBooking Driver</p>
        <h1 class="fw-bold">My assigned trips</h1>
    </div>
    <form asp-controller="DriverAuth" asp-action="Logout" method="post">
        @Html.AntiForgeryToken()
        <button type="submit" class="btn btn-outline-secondary rounded-pill">Sign out</button>
    </form>
</div>

<div class="ride-card mb-4 d-flex justify-content-between align-items-center">
    <div>
        <p class="fw-semibold mb-1">Location sharing</p>
        <p class="text-muted small mb-0">Share your live location with dispatch while this page stays open.</p>
    </div>
    <button id="locationToggle" type="button" class="btn ride-btn-primary" data-booking-id="@activeBooking?.BookingId">Share my location</button>
</div>

@if (!Model.Any())
{
    <div class="ride-card text-center py-5">
        <h2 class="h5 fw-bold">No trips assigned</h2>
        <p class="text-muted mb-0">Your dispatcher will assign a trip to your phone number.</p>
    </div>
}

@foreach (var trip in Model)
{
    <div class="ride-card mb-3">
        <div class="d-flex justify-content-between flex-wrap gap-2">
            <div>
                <h2 class="h5 fw-bold mb-0">@trip.CustomerName</h2>
                <p class="text-muted small mb-0">@trip.BookingReference &middot; @trip.CustomerPhone</p>
            </div>
            <div class="text-end">
                <p class="fw-bold mb-0">@trip.PickupDate.ToString("yyyy-MM-dd") &middot; @trip.PickupTime.ToString("HH:mm")</p>
                <span class="ride-pill">@trip.AssignmentStatus / @trip.BookingStatus</span>
            </div>
        </div>
        <p class="mt-2 mb-1">@trip.PickupLocation &rarr; @trip.Destination</p>
        <p class="text-muted small">@trip.Passengers pax &middot; @trip.Bags bags</p>
        @if (!string.IsNullOrEmpty(trip.Notes))
        {
            <p class="text-muted small">Notes: @trip.Notes</p>
        }

        @if (trip.AssignmentStatus == "Pending")
        {
            <div class="d-flex gap-2 mt-2">
                <form asp-controller="Driver" asp-action="Accept" method="post">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="assignmentId" value="@trip.AssignmentId" />
                    <button type="submit" class="btn ride-btn-primary">Accept</button>
                </form>
                <form asp-controller="Driver" asp-action="Reject" method="post">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="assignmentId" value="@trip.AssignmentId" />
                    <button type="submit" class="btn btn-outline-secondary">Reject</button>
                </form>
            </div>
        }
        else if (trip.AssignmentStatus == "Accepted" && trip.BookingStatus != "Completed")
        {
            <form asp-controller="Driver" asp-action="UpdateStatus" method="post" class="d-flex gap-2 mt-2">
                @Html.AntiForgeryToken()
                <input type="hidden" name="bookingId" value="@trip.BookingId" />
                <select name="newStatus" class="form-select ride-input">
                    <option value="Picked_Up">Picked Up</option>
                    <option value="In_Transit">In Transit</option>
                    <option value="Dropped_Off">Dropped Off</option>
                    <option value="Completed">Completed</option>
                </select>
                <button type="submit" class="btn ride-btn-primary text-nowrap">Update status</button>
            </form>
        }
    </div>
}

@section Scripts {
    <script src="~/js/driver-location.js" asp-append-version="true"></script>
}
```

- [ ] **Step 12: Add the browser geolocation reporting script**

```js
// wwwroot/js/driver-location.js
(function () {
    var button = document.getElementById('locationToggle');
    if (!button) {
        return;
    }

    var intervalId = null;
    var bookingId = button.dataset.bookingId || '';

    function reportPosition() {
        if (!navigator.geolocation) {
            return;
        }

        navigator.geolocation.getCurrentPosition(function (position) {
            var payload = {
                bookingId: bookingId ? parseInt(bookingId, 10) : null,
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                accuracyMeters: position.coords.accuracy ? Math.round(position.coords.accuracy) : null,
                speedKmh: position.coords.speed ? position.coords.speed * 3.6 : null
            };

            fetch('/Driver/ReportLocation', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            }).catch(function (err) {
                console.warn('Failed to report location:', err);
            });
        }, function (error) {
            console.warn('Location error:', error.message);
        });
    }

    button.addEventListener('click', function () {
        if (intervalId) {
            clearInterval(intervalId);
            intervalId = null;
            button.textContent = 'Share my location';
        } else {
            reportPosition();
            intervalId = setInterval(reportPosition, 30000);
            button.textContent = 'Stop sharing location';
        }
    });
})();
```

- [ ] **Step 13: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 14: Commit**

```bash
git add ViewModels/ Services/ Controllers/DriverAuthController.cs Controllers/DriverController.cs Tests/ Views/DriverAuth/ Views/Driver/ wwwroot/js/driver-location.js Program.cs
git commit -m "feat: add driver portal with accept/reject, status updates, and browser location reporting"
```

---

## Part 6: Notifications & Background Jobs

### Task 8: Notification Service (Email, WhatsApp, Google Calendar)

**Files:**
- Modify: `appsettings.json`
- Create: `Services/EmailSettings.cs`
- Create: `Services/WhatsAppSettings.cs`
- Create: `Services/GoogleCalendarSettings.cs`
- Create: `Services/IEmailSender.cs`
- Create: `Services/SmtpEmailSender.cs`
- Create: `Services/IWhatsAppSender.cs`
- Create: `Services/WhatsAppCloudApiSender.cs`
- Create: `Services/ICalendarSyncService.cs`
- Create: `Services/GoogleCalendarSyncService.cs`
- Create: `Services/INotificationService.cs`
- Create: `Services/NotificationService.cs`
- Create: `Tests/Services/NotificationServiceTests.cs`
- Modify: `Services/IDriverPortalService.cs`
- Modify: `Services/DriverPortalService.cs`
- Modify: `Tests/Services/DriverPortalServiceTests.cs`
- Modify: `Controllers/BookingController.cs`
- Modify: `Controllers/AdminController.cs`
- Modify: `Controllers/DriverController.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: SMTP (MailKit), WhatsApp Cloud API (Meta Graph API, HTTP), Google Calendar API (service account)
- Produces: `INotificationService`, persists `Notification` rows with `DeliveryStatus` for Task 9's retry job to pick up

> Per spec §7.2, only Email, WhatsApp, and Google Calendar are in scope for Phase 1 — SMS and Push are Phase 2.

- [ ] **Step 1: Fix configuration and extend settings**

The existing `WhatsAppSettings.ApiUrl` in `appsettings.json` points at the wrong host (`graph.instagram.com` instead of the WhatsApp Cloud API host), and `GoogleCalendarSettings` is shaped for an interactive OAuth flow that doesn't fit a server-side operator calendar. Replace both sections and extend `EmailSettings`:

```json
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "noreply@ridebooking.my",
    "SenderName": "RideBooking",
    "SmtpUsername": "YOUR_SMTP_USERNAME",
    "SmtpPassword": "YOUR_SMTP_PASSWORD",
    "OperatorEmail": "operator@ridebooking.my"
  },
  "WhatsAppSettings": {
    "ApiUrl": "https://graph.facebook.com/v18.0",
    "AccessToken": "YOUR_ACCESS_TOKEN",
    "PhoneNumberId": "YOUR_PHONE_NUMBER_ID"
  },
  "GoogleCalendarSettings": {
    "ServiceAccountKeyPath": "google-service-account.json",
    "CalendarId": "primary"
  }
```

(Remove the old `BusinessAccountId`, `ClientId`, `ClientSecret`, and `RedirectUri` entries — a service account needs neither an OAuth client nor a redirect URI.)

```csharp
// Services/EmailSettings.cs
namespace RideBooking.Services
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string OperatorEmail { get; set; } = string.Empty;
    }
}
```

```csharp
// Services/WhatsAppSettings.cs
namespace RideBooking.Services
{
    public class WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
    }
}
```

```csharp
// Services/GoogleCalendarSettings.cs
namespace RideBooking.Services
{
    public class GoogleCalendarSettings
    {
        public string ServiceAccountKeyPath { get; set; } = string.Empty;
        public string CalendarId { get; set; } = "primary";
    }
}
```

- [ ] **Step 2: Create the channel sender interfaces and implementations**

```csharp
// Services/IEmailSender.cs
namespace RideBooking.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string body);
    }
}
```

```csharp
// Services/SmtpEmailSender.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RideBooking.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            if (!string.IsNullOrEmpty(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
```

```csharp
// Services/IWhatsAppSender.cs
namespace RideBooking.Services
{
    public interface IWhatsAppSender
    {
        Task SendAsync(string toPhone, string message);
    }
}
```

```csharp
// Services/WhatsAppCloudApiSender.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace RideBooking.Services
{
    public class WhatsAppCloudApiSender : IWhatsAppSender
    {
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;

        public WhatsAppCloudApiSender(HttpClient httpClient, IOptions<WhatsAppSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task SendAsync(string toPhone, string message)
        {
            var url = $"{_settings.ApiUrl}/{_settings.PhoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                to = NormalizePhone(toPhone),
                type = "text",
                text = new { body = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        internal static string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.StartsWith('0') ? "60" + digits[1..] : digits;
        }
    }
}
```

```csharp
// Services/ICalendarSyncService.cs
using RideBooking.Models;

namespace RideBooking.Services
{
    public interface ICalendarSyncService
    {
        Task CreateOrUpdateEventAsync(Booking booking);
    }
}
```

```csharp
// Services/GoogleCalendarSyncService.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;

namespace RideBooking.Services
{
    public class GoogleCalendarSyncService : ICalendarSyncService
    {
        private readonly GoogleCalendarSettings _settings;
        private readonly RideBookingDbContext _context;

        public GoogleCalendarSyncService(IOptions<GoogleCalendarSettings> settings, RideBookingDbContext context)
        {
            _settings = settings.Value;
            _context = context;
        }

        public async Task CreateOrUpdateEventAsync(Booking booking)
        {
            var credential = GoogleCredential.FromFile(_settings.ServiceAccountKeyPath)
                .CreateScoped(CalendarService.Scope.Calendar);

            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RideBooking"
            });

            var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
            var calendarEvent = new Event
            {
                Summary = $"{booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination}",
                Description = $"Passengers: {booking.Passengers}, Bags: {booking.Bags}, Vehicle: {booking.RequestedVehicleType}",
                Start = new EventDateTime { DateTimeDateTimeOffset = pickupAt },
                End = new EventDateTime { DateTimeDateTimeOffset = pickupAt.AddHours(2) }
            };

            var existing = await _context.OperatorCalendarEvents.FirstOrDefaultAsync(e => e.BookingId == booking.Id);

            if (existing?.GoogleEventId != null)
            {
                await service.Events.Update(calendarEvent, _settings.CalendarId, existing.GoogleEventId).ExecuteAsync();
                existing.SyncedAt = DateTime.UtcNow;
            }
            else
            {
                var created = await service.Events.Insert(calendarEvent, _settings.CalendarId).ExecuteAsync();
                _context.OperatorCalendarEvents.Add(new OperatorCalendarEvent
                {
                    BookingId = booking.Id,
                    GoogleEventId = created.Id,
                    SyncedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 3: Write a failing test for NotificationService**

```csharp
// Tests/Services/NotificationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task SendAsync(string toEmail, string subject, string body)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("SMTP unavailable");
            }
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    public class FakeWhatsAppSender : IWhatsAppSender
    {
        public List<(string To, string Message)> Sent { get; } = new();

        public Task SendAsync(string toPhone, string message)
        {
            Sent.Add((toPhone, message));
            return Task.CompletedTask;
        }
    }

    public class FakeCalendarSyncService : ICalendarSyncService
    {
        public int CallCount { get; private set; }

        public Task CreateOrUpdateEventAsync(Booking booking)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    public class NotificationServiceTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideBookingDbContext context)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0004",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private static IOptions<EmailSettings> Settings() => Options.Create(new EmailSettings
        {
            SenderEmail = "noreply@ridebooking.my",
            SenderName = "RideBooking",
            OperatorEmail = "operator@ridebooking.my"
        });

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
            Assert.Contains(emailSender.Sent, s => s.To == "operator@ridebooking.my");
            Assert.Equal(1, calendarSync.CallCount);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(3, notifications.Count);
            Assert.All(notifications, n => Assert.Equal("Sent", n.DeliveryStatus));
        }

        [Fact]
        public async Task SendBookingCreatedNotificationAsync_WhenEmailFails_LogsFailedNotificationAndDoesNotThrow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var emailSender = new FakeEmailSender { ShouldThrow = true };
            var whatsAppSender = new FakeWhatsAppSender();
            var calendarSync = new FakeCalendarSyncService();
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings());

            // Act
            await service.SendBookingCreatedNotificationAsync(booking.Id);

            // Assert (no exception thrown)
            var failed = await context.Notifications
                .Where(n => n.BookingId == booking.Id && n.Channel == "Email")
                .ToListAsync();
            Assert.All(failed, n => Assert.Equal("Failed", n.DeliveryStatus));
            Assert.All(failed, n => Assert.Equal("SMTP unavailable", n.ErrorMessage));
        }

        [Fact]
        public async Task SendDriverAssignedNotificationAsync_SendsWhatsAppToDriverAndEmailToOperator()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings());

            // Act
            await service.SendDriverAssignedNotificationAsync(booking.Id, driver.Id);

            // Assert
            Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0123456789", whatsAppSender.Sent[0].To);
            Assert.Single(emailSender.Sent);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~NotificationServiceTests`
Expected: FAIL (`NotificationService` does not exist)

- [ ] **Step 5: Implement INotificationService and NotificationService**

```csharp
// Services/INotificationService.cs
namespace RideBooking.Services
{
    public interface INotificationService
    {
        Task SendBookingCreatedNotificationAsync(int bookingId);
        Task SendDriverAssignedNotificationAsync(int bookingId, int driverId);
        Task SendDriverAcceptedNotificationAsync(int bookingId);
        Task SendBookingCompletedNotificationAsync(int bookingId);
        Task SendBookingCancelledNotificationAsync(int bookingId);
    }
}
```

```csharp
// Services/NotificationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;

namespace RideBooking.Services
{
    public class NotificationService : INotificationService
    {
        private readonly RideBookingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;

        public NotificationService(
            RideBookingDbContext context,
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

        public async Task SendBookingCreatedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var customerMessage = $"Hi {booking.Customer!.Name}, your RideBooking reference is {booking.BookingReference}. We'll contact you to confirm your driver.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCreated", customerMessage,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideBooking reservation", customerMessage));

            var operatorMessage = $"New booking {booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}.";
            await SendAndLogAsync(bookingId, "Operator", null, "Email", "BookingCreated", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, "Calendar", "BookingCreated", "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }

        public async Task SendDriverAssignedNotificationAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");
            var driver = await _context.Drivers.FindAsync(driverId)
                ?? throw new InvalidOperationException($"Driver {driverId} not found");

            var driverMessage = $"New job {booking.BookingReference}: pickup {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}. Log in to the Driver Portal to accept or reject.";
            await SendAndLogAsync(bookingId, "Driver", driverId, "WhatsApp", "DriverAssigned", driverMessage,
                () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));

            var operatorMessage = $"Driver {driver.Name} assigned to booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Operator", null, "Email", "DriverAssigned", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "Driver assigned", operatorMessage));
        }

        public async Task SendDriverAcceptedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Good news! A driver has been confirmed for your booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "DriverAccepted", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Driver confirmed", message));
        }

        public async Task SendBookingCompletedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Thanks for riding with RideBooking! Your trip {booking.BookingReference} is complete.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCompleted", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Trip complete", message));
        }

        public async Task SendBookingCancelledNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} has been cancelled.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, "Email", "BookingCancelled", message,
                () => _emailSender.SendAsync(booking.Customer!.Email, "Booking cancelled", message));

            var latestDriverId = await _context.DriverAssignments
                .Where(a => a.BookingId == bookingId && a.AssignmentStatus != "Rejected")
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => (int?)a.DriverId)
                .FirstOrDefaultAsync();

            if (latestDriverId != null)
            {
                var driver = await _context.Drivers.FindAsync(latestDriverId.Value);
                if (driver != null)
                {
                    var driverMessage = $"Booking {booking.BookingReference} has been cancelled. No action needed.";
                    await SendAndLogAsync(bookingId, "Driver", driver.Id, "WhatsApp", "BookingCancelled", driverMessage,
                        () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));
                }
            }
        }

        private async Task SendAndLogAsync(
            int bookingId, string recipientType, int? recipientId, string channel, string eventType,
            string messageContent, Func<Task> send)
        {
            var notification = new Notification
            {
                BookingId = bookingId,
                RecipientType = recipientType,
                RecipientId = recipientId,
                Channel = channel,
                EventType = eventType,
                MessageContent = messageContent,
                DeliveryStatus = "Pending"
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                await send();
                notification.DeliveryStatus = "Sent";
                notification.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.DeliveryStatus = "Failed";
                notification.ErrorMessage = ex.Message;
            }

            await _context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~NotificationServiceTests`
Expected: PASS (3 tests)

- [ ] **Step 7: Wire notification sends into the booking, assignment, and acceptance flows**

`AcceptAssignmentAsync` needs to hand the caller a booking id so a notification can be sent. In `Services/IDriverPortalService.cs`, change the signature:

```csharp
// Before:
        Task AcceptAssignmentAsync(int assignmentId, int driverId);

// After:
        Task<int> AcceptAssignmentAsync(int assignmentId, int driverId);
```

In `Services/DriverPortalService.cs`, change `AcceptAssignmentAsync` to return the booking id:

```csharp
        public async Task<int> AcceptAssignmentAsync(int assignmentId, int driverId)
        {
            var assignment = await GetOwnedAssignmentAsync(assignmentId, driverId);
            var booking = assignment.Booking ?? await _context.Bookings.FindAsync(assignment.BookingId)
                ?? throw new InvalidOperationException($"Booking {assignment.BookingId} not found");

            assignment.AssignmentStatus = "Accepted";
            assignment.AcceptedAt = DateTime.UtcNow;

            var previousStatus = booking.Status;
            booking.Status = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;

            _context.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = previousStatus,
                NewStatus = "Confirmed",
                ChangedBy = "Driver"
            });

            await _context.SaveChangesAsync();
            return booking.Id;
        }
```

In `Tests/Services/DriverPortalServiceTests.cs`, update the two tests that call `AcceptAssignmentAsync` to capture the return value: change `await service.AcceptAssignmentAsync(assignment.Id, driver.Id);` to `var bookingId = await service.AcceptAssignmentAsync(assignment.Id, driver.Id);` and add `Assert.Equal(booking.Id, bookingId);` in `AcceptAssignmentAsync_SetsAssignmentAcceptedAndBookingConfirmed`.

In `Controllers/BookingController.cs`, inject `INotificationService` and send after a successful booking:

```csharp
// Before:
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

// After:
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;

        public BookingController(IBookingService bookingService, INotificationService notificationService)
        {
            _bookingService = bookingService;
            _notificationService = notificationService;
        }
```

```csharp
// Before (inside the POST Create try block):
                var booking = await _bookingService.CreateBookingAsync(request);
                TempData["BookingReference"] = booking.BookingReference;
                return RedirectToAction(nameof(Confirmation));

// After:
                var booking = await _bookingService.CreateBookingAsync(request);
                await _notificationService.SendBookingCreatedNotificationAsync(booking.Id);
                TempData["BookingReference"] = booking.BookingReference;
                return RedirectToAction(nameof(Confirmation));
```

In `Controllers/AdminController.cs`, inject `INotificationService` and send on assignment and on cancel/complete status changes:

```csharp
// Before:
        private readonly IDriverAssignmentService _driverAssignmentService;

        public AdminController(IDriverAssignmentService driverAssignmentService)
        {
            _driverAssignmentService = driverAssignmentService;
        }

// After:
        private readonly IDriverAssignmentService _driverAssignmentService;
        private readonly INotificationService _notificationService;

        public AdminController(IDriverAssignmentService driverAssignmentService, INotificationService notificationService)
        {
            _driverAssignmentService = driverAssignmentService;
            _notificationService = notificationService;
        }
```

```csharp
// Before:
        public async Task<IActionResult> AssignDriver(AssignDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.AssignDriverAsync(model.BookingId, model.DriverId);
            }
            return RedirectToAction(nameof(Index));
        }

// After:
        public async Task<IActionResult> AssignDriver(AssignDriverViewModel model)
        {
            if (ModelState.IsValid)
            {
                await _driverAssignmentService.AssignDriverAsync(model.BookingId, model.DriverId);
                await _notificationService.SendDriverAssignedNotificationAsync(model.BookingId, model.DriverId);
            }
            return RedirectToAction(nameof(Index));
        }
```

```csharp
// Before:
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            await _driverAssignmentService.UpdateBookingStatusAsync(bookingId, newStatus, User.Identity?.Name ?? "Admin");
            return RedirectToAction(nameof(Index));
        }

// After:
        public async Task<IActionResult> UpdateStatus(int bookingId, string newStatus)
        {
            await _driverAssignmentService.UpdateBookingStatusAsync(bookingId, newStatus, User.Identity?.Name ?? "Admin");

            if (newStatus == "Cancelled")
            {
                await _notificationService.SendBookingCancelledNotificationAsync(bookingId);
            }
            else if (newStatus == "Completed")
            {
                await _notificationService.SendBookingCompletedNotificationAsync(bookingId);
            }

            return RedirectToAction(nameof(Index));
        }
```

In `Controllers/DriverController.cs`, inject `INotificationService` and send on accept:

```csharp
// Before:
        private readonly IDriverPortalService _driverPortalService;

        public DriverController(IDriverPortalService driverPortalService)
        {
            _driverPortalService = driverPortalService;
        }

// After:
        private readonly IDriverPortalService _driverPortalService;
        private readonly INotificationService _notificationService;

        public DriverController(IDriverPortalService driverPortalService, INotificationService notificationService)
        {
            _driverPortalService = driverPortalService;
            _notificationService = notificationService;
        }
```

```csharp
// Before:
        public async Task<IActionResult> Accept(int assignmentId)
        {
            await _driverPortalService.AcceptAssignmentAsync(assignmentId, GetCurrentDriverId());
            return RedirectToAction(nameof(Index));
        }

// After:
        public async Task<IActionResult> Accept(int assignmentId)
        {
            var bookingId = await _driverPortalService.AcceptAssignmentAsync(assignmentId, GetCurrentDriverId());
            await _notificationService.SendDriverAcceptedNotificationAsync(bookingId);
            return RedirectToAction(nameof(Index));
        }
```

`BookingControllerTests`, `AdminControllerTests`, and `DriverControllerTests` (Steps 5-9 of Tasks 4, 6, 7) construct their controllers directly with a single service argument — each now needs a second `INotificationService` argument to keep compiling. Add this private helper to all three test classes and use it in place of `Options.Create(new EmailSettings { ... })` calls scattered inline:

```csharp
        private static INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(
                context,
                new RideBooking.Tests.Services.FakeEmailSender(),
                new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    SenderEmail = "noreply@ridebooking.my",
                    SenderName = "RideBooking",
                    OperatorEmail = "operator@ridebooking.my"
                }));
```

In `Tests/Controllers/BookingControllerTests.cs`, change:

```csharp
            var controller = new BookingController(service)
```

to:

```csharp
            var controller = new BookingController(service, BuildNotificationService(context))
```

(in all three test methods: `Create_Post_WithValidRequest_RedirectsToConfirmation`, `Create_Post_WithPastPickupDate_ReturnsViewWithError`, `Create_Post_WithInvalidModelState_ReturnsViewWithSameModel`).

In `Tests/Controllers/AdminControllerTests.cs`, change both:

```csharp
            var controller = new AdminController(service);
```

to:

```csharp
            var controller = new AdminController(service, BuildNotificationService(context));
```

In `Tests/Controllers/DriverControllerTests.cs`, change the `WithAuthenticatedDriver` helper:

```csharp
        private static DriverController WithAuthenticatedDriver(IDriverPortalService service, int driverId)
        {
            var controller = new DriverController(service)
```

to:

```csharp
        private static DriverController WithAuthenticatedDriver(RideBookingDbContext context, IDriverPortalService service, int driverId)
        {
            var controller = new DriverController(service, BuildNotificationService(context))
```

and its one call site:

```csharp
            var controller = WithAuthenticatedDriver(service, driver.Id);
```

to:

```csharp
            var controller = WithAuthenticatedDriver(context, service, driver.Id);
```

- [ ] **Step 8: Register the new services in Program.cs**

```csharp
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsAppSettings"));
builder.Services.Configure<GoogleCalendarSettings>(builder.Configuration.GetSection("GoogleCalendarSettings"));

builder.Services.AddHttpClient<WhatsAppCloudApiSender>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IWhatsAppSender>(sp => sp.GetRequiredService<WhatsAppCloudApiSender>());
builder.Services.AddScoped<ICalendarSyncService, GoogleCalendarSyncService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
```

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 10: Commit**

```bash
git add appsettings.json Services/ Controllers/ Tests/ Program.cs
git commit -m "feat: add email, WhatsApp, and Google Calendar notifications"
```

---

### Task 9: Background Jobs (Quartz.NET — Retries, Reminders, No-Show Detection)

**Files:**
- Modify: `Models/Notification.cs`
- Create: `Jobs/NotificationRetryJob.cs`
- Create: `Jobs/ReminderEscalationJob.cs`
- Create: `Jobs/NoShowDetectionJob.cs`
- Create: `Tests/Jobs/NotificationRetryJobTests.cs`
- Create: `Tests/Jobs/ReminderEscalationJobTests.cs`
- Create: `Tests/Jobs/NoShowDetectionJobTests.cs`
- Modify: `Services/INotificationService.cs`
- Modify: `Services/NotificationService.cs`
- Modify: `Tests/Services/NotificationServiceTests.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: `RideBookingDbContext`, `IEmailSender`, `IWhatsAppSender`, `INotificationService`
- Produces: three Quartz `IJob` implementations, scheduled every 5 minutes

> Per spec §7.3/§10: retry delays 5min → 15min → 1hr → 3hr, max 4 retries then dead-letter; escalation reminders at 1hr and 30min before pickup for unassigned bookings; auto no-show 30+ minutes after pickup time with no "Picked Up" status.

- [ ] **Step 1: Add retry-tracking fields to Notification**

```csharp
// Models/Notification.cs
namespace RideBooking.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string RecipientType { get; set; } = string.Empty; // Customer, Driver, Operator
        public int? RecipientId { get; set; }
        public string RecipientContact { get; set; } = string.Empty; // email address or phone number, used for retries
        public string Channel { get; set; } = string.Empty; // Email, WhatsApp, SMS, Push, Calendar
        public string EventType { get; set; } = string.Empty;
        public string? Subject { get; set; } // used for Email retries
        public string? MessageContent { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Failed, DeadLetter
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Create the migration**

Run: `dotnet ef migrations add AddNotificationRetryFields`
Run: `dotnet ef database update`

- [ ] **Step 3: Extend INotificationService and rewrite NotificationService to log retry fields and add two new notification types**

```csharp
// Services/INotificationService.cs
namespace RideBooking.Services
{
    public interface INotificationService
    {
        Task SendBookingCreatedNotificationAsync(int bookingId);
        Task SendDriverAssignedNotificationAsync(int bookingId, int driverId);
        Task SendDriverAcceptedNotificationAsync(int bookingId);
        Task SendBookingCompletedNotificationAsync(int bookingId);
        Task SendBookingCancelledNotificationAsync(int bookingId);
        Task SendUnassignedReminderAsync(int bookingId, bool urgent);
        Task SendNoShowNotificationAsync(int bookingId);
    }
}
```

Replace the entire contents of `Services/NotificationService.cs`:

```csharp
// Services/NotificationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Models;

namespace RideBooking.Services
{
    public class NotificationService : INotificationService
    {
        private readonly RideBookingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly EmailSettings _emailSettings;

        public NotificationService(
            RideBookingDbContext context,
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

        public async Task SendBookingCreatedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var customerMessage = $"Hi {booking.Customer!.Name}, your RideBooking reference is {booking.BookingReference}. We'll contact you to confirm your driver.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer.Email, "Email", "BookingCreated",
                "Your RideBooking reservation", customerMessage,
                () => _emailSender.SendAsync(booking.Customer.Email, "Your RideBooking reservation", customerMessage));

            var operatorMessage = $"New booking {booking.BookingReference}: {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}.";
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "BookingCreated",
                "New booking received", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "New booking received", operatorMessage));

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Calendar", "BookingCreated",
                null, "Calendar event created",
                () => _calendarSyncService.CreateOrUpdateEventAsync(booking));
        }

        public async Task SendDriverAssignedNotificationAsync(int bookingId, int driverId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");
            var driver = await _context.Drivers.FindAsync(driverId)
                ?? throw new InvalidOperationException($"Driver {driverId} not found");

            var driverMessage = $"New job {booking.BookingReference}: pickup {booking.PickupLocation} -> {booking.Destination} on {booking.PickupDate:yyyy-MM-dd} {booking.PickupTime:HH:mm}. Log in to the Driver Portal to accept or reject.";
            await SendAndLogAsync(bookingId, "Driver", driverId, driver.Phone, "WhatsApp", "DriverAssigned",
                null, driverMessage,
                () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));

            var operatorMessage = $"Driver {driver.Name} assigned to booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", "DriverAssigned",
                "Driver assigned", operatorMessage,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, "Driver assigned", operatorMessage));
        }

        public async Task SendDriverAcceptedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Good news! A driver has been confirmed for your booking {booking.BookingReference}.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "DriverAccepted",
                "Driver confirmed", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Driver confirmed", message));
        }

        public async Task SendBookingCompletedNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Thanks for riding with RideBooking! Your trip {booking.BookingReference} is complete.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "BookingCompleted",
                "Trip complete", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Trip complete", message));
        }

        public async Task SendBookingCancelledNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} has been cancelled.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "BookingCancelled",
                "Booking cancelled", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Booking cancelled", message));

            var latestDriverId = await _context.DriverAssignments
                .Where(a => a.BookingId == bookingId && a.AssignmentStatus != "Rejected")
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => (int?)a.DriverId)
                .FirstOrDefaultAsync();

            if (latestDriverId != null)
            {
                var driver = await _context.Drivers.FindAsync(latestDriverId.Value);
                if (driver != null)
                {
                    var driverMessage = $"Booking {booking.BookingReference} has been cancelled. No action needed.";
                    await SendAndLogAsync(bookingId, "Driver", driver.Id, driver.Phone, "WhatsApp", "BookingCancelled",
                        null, driverMessage,
                        () => _whatsAppSender.SendAsync(driver.Phone, driverMessage));
                }
            }
        }

        public async Task SendUnassignedReminderAsync(int bookingId, bool urgent)
        {
            var booking = await _context.Bookings.FindAsync(bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var eventType = urgent ? "Escalation_30min" : "Reminder_1hr";
            var message = urgent
                ? $"URGENT: booking {booking.BookingReference} has no driver assigned and pickup is in 30 minutes."
                : $"Reminder: booking {booking.BookingReference} has no driver assigned and pickup is in 1 hour.";

            await SendAndLogAsync(bookingId, "Operator", null, _emailSettings.OperatorEmail, "Email", eventType,
                eventType, message,
                () => _emailSender.SendAsync(_emailSettings.OperatorEmail, eventType, message));
        }

        public async Task SendNoShowNotificationAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.Id == bookingId)
                ?? throw new InvalidOperationException($"Booking {bookingId} not found");

            var message = $"Your booking {booking.BookingReference} was marked as a no-show because pickup wasn't confirmed 30 minutes after the scheduled time.";
            await SendAndLogAsync(bookingId, "Customer", booking.CustomerId, booking.Customer!.Email, "Email", "NoShow",
                "Booking marked as no-show", message,
                () => _emailSender.SendAsync(booking.Customer.Email, "Booking marked as no-show", message));
        }

        private async Task SendAndLogAsync(
            int bookingId, string recipientType, int? recipientId, string recipientContact, string channel,
            string eventType, string? subject, string messageContent, Func<Task> send)
        {
            var notification = new Notification
            {
                BookingId = bookingId,
                RecipientType = recipientType,
                RecipientId = recipientId,
                RecipientContact = recipientContact,
                Channel = channel,
                EventType = eventType,
                Subject = subject,
                MessageContent = messageContent,
                DeliveryStatus = "Pending"
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                await send();
                notification.DeliveryStatus = "Sent";
                notification.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.DeliveryStatus = "Failed";
                notification.LastAttemptAt = DateTime.UtcNow;
                notification.ErrorMessage = ex.Message;
            }

            await _context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 4: Run the existing notification tests to confirm they still pass unchanged**

Run: `dotnet test --filter FullyQualifiedName~NotificationServiceTests`
Expected: PASS (3 tests — `SendAndLogAsync`'s new parameters don't change any test's observable behavior)

- [ ] **Step 5: Write failing tests for NotificationRetryJob**

```csharp
// Tests/Jobs/NotificationRetryJobTests.cs
using Microsoft.EntityFrameworkCore;
using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Models;
using RideBooking.Tests.Services;
using Xunit;

namespace RideBooking.Tests.Jobs
{
    public class NotificationRetryJobTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Notification> SeedFailedNotificationAsync(RideBookingDbContext context, int retryCount, DateTime lastAttemptAt)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0005",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = new DateOnly(2026, 9, 10),
                PickupTime = new TimeOnly(9, 0),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var notification = new Notification
            {
                BookingId = booking.Id,
                RecipientType = "Customer",
                RecipientContact = "sim@email.com",
                Channel = "Email",
                EventType = "BookingCreated",
                Subject = "Your RideBooking reservation",
                MessageContent = "Hi Uncle Sim",
                DeliveryStatus = "Failed",
                RetryCount = retryCount,
                LastAttemptAt = lastAttemptAt
            };
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            return notification;
        }

        [Fact]
        public async Task RunAsync_WhenBackoffElapsedAndResendSucceeds_MarksAsSent()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 0, lastAttemptAt: DateTime.UtcNow.AddMinutes(-10));
            var emailSender = new FakeEmailSender();
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("Sent", updated!.DeliveryStatus);
            Assert.Equal(1, updated.RetryCount);
            Assert.Single(emailSender.Sent);
        }

        [Fact]
        public async Task RunAsync_WhenBackoffNotYetElapsed_DoesNotRetry()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 0, lastAttemptAt: DateTime.UtcNow.AddMinutes(-1));
            var emailSender = new FakeEmailSender();
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("Failed", updated!.DeliveryStatus);
            Assert.Equal(0, updated.RetryCount);
            Assert.Empty(emailSender.Sent);
        }

        [Fact]
        public async Task RunAsync_OnTheFourthRetryStillFailing_MarksAsDeadLetter()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var notification = await SeedFailedNotificationAsync(context, retryCount: 3, lastAttemptAt: DateTime.UtcNow.AddHours(-4));
            var emailSender = new FakeEmailSender { ShouldThrow = true };
            var job = new NotificationRetryJob(context, emailSender, new FakeWhatsAppSender());

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.Equal("DeadLetter", updated!.DeliveryStatus);
            Assert.Equal(4, updated.RetryCount);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~NotificationRetryJobTests`
Expected: FAIL (`RideBooking.Jobs` namespace / `NotificationRetryJob` does not exist)

- [ ] **Step 7: Implement NotificationRetryJob**

```csharp
// Jobs/NotificationRetryJob.cs
using Microsoft.EntityFrameworkCore;
using Quartz;
using RideBooking.Data;
using RideBooking.Services;

namespace RideBooking.Jobs
{
    public class NotificationRetryJob : IJob
    {
        private static readonly TimeSpan[] BackoffDelays =
        {
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(3)
        };

        private readonly RideBookingDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWhatsAppSender _whatsAppSender;

        public NotificationRetryJob(RideBookingDbContext context, IEmailSender emailSender, IWhatsAppSender whatsAppSender)
        {
            _context = context;
            _emailSender = emailSender;
            _whatsAppSender = whatsAppSender;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.Notifications
                .Where(n => n.DeliveryStatus == "Failed" && n.RetryCount < 4)
                .ToListAsync();

            foreach (var notification in candidates)
            {
                var delay = BackoffDelays[notification.RetryCount];
                if (notification.LastAttemptAt == null || now - notification.LastAttemptAt.Value < delay)
                {
                    continue;
                }

                notification.RetryCount++;
                notification.LastAttemptAt = now;

                try
                {
                    if (notification.Channel == "Email")
                    {
                        await _emailSender.SendAsync(notification.RecipientContact, notification.Subject ?? notification.EventType, notification.MessageContent ?? string.Empty);
                    }
                    else if (notification.Channel == "WhatsApp")
                    {
                        await _whatsAppSender.SendAsync(notification.RecipientContact, notification.MessageContent ?? string.Empty);
                    }

                    notification.DeliveryStatus = "Sent";
                    notification.SentAt = now;
                }
                catch (Exception ex)
                {
                    notification.ErrorMessage = ex.Message;
                    notification.DeliveryStatus = notification.RetryCount >= 4 ? "DeadLetter" : "Failed";
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~NotificationRetryJobTests`
Expected: PASS (3 tests)

- [ ] **Step 9: Write and implement ReminderEscalationJob**

```csharp
// Tests/Jobs/ReminderEscalationJobTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Jobs
{
    public class ReminderEscalationJobTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedUnassignedBookingAsync(RideBookingDbContext context, DateTime pickupAtUtc)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0006",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = DateOnly.FromDateTime(pickupAtUtc),
                PickupTime = TimeOnly.FromDateTime(pickupAtUtc),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(context, new RideBooking.Tests.Services.FakeEmailSender(), new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@ridebooking.my", SenderName = "RideBooking", OperatorEmail = "operator@ridebooking.my" }));

        [Fact]
        public async Task RunAsync_WithUnassignedBookingOneHourOut_SendsReminderOnce()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();
            await job.RunAsync(); // second run should not duplicate

            // Assert
            var count = await context.Notifications.CountAsync(n => n.EventType == "Reminder_1hr");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task RunAsync_WithUnassignedBookingThirtyMinutesOut_SendsUrgentEscalation()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddMinutes(30));
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var count = await context.Notifications.CountAsync(n => n.EventType == "Escalation_30min");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task RunAsync_WithAssignedBooking_DoesNotSendReminder()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedUnassignedBookingAsync(context, DateTime.UtcNow.AddHours(1));
            booking.Status = "Driver_Assigned";
            await context.SaveChangesAsync();
            var job = new ReminderEscalationJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var count = await context.Notifications.CountAsync(n => n.BookingId == booking.Id);
            Assert.Equal(0, count);
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~ReminderEscalationJobTests`
Expected: FAIL (`ReminderEscalationJob` does not exist)

```csharp
// Jobs/ReminderEscalationJob.cs
using Microsoft.EntityFrameworkCore;
using Quartz;
using RideBooking.Data;
using RideBooking.Services;

namespace RideBooking.Jobs
{
    public class ReminderEscalationJob : IJob
    {
        private readonly RideBookingDbContext _context;
        private readonly INotificationService _notificationService;

        public ReminderEscalationJob(RideBookingDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            await CheckWindowAsync(now, TimeSpan.FromHours(1), "Reminder_1hr", urgent: false);
            await CheckWindowAsync(now, TimeSpan.FromMinutes(30), "Escalation_30min", urgent: true);
        }

        private async Task CheckWindowAsync(DateTime now, TimeSpan window, string eventType, bool urgent)
        {
            var windowStart = now.Add(window).AddMinutes(-2);
            var windowEnd = now.Add(window).AddMinutes(2);

            var unassigned = await _context.Bookings
                .Where(b => b.Status == "New" || b.Status == "Confirmed")
                .ToListAsync();

            foreach (var booking in unassigned)
            {
                var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
                if (pickupAt < windowStart || pickupAt > windowEnd)
                {
                    continue;
                }

                var alreadySent = await _context.Notifications
                    .AnyAsync(n => n.BookingId == booking.Id && n.EventType == eventType);
                if (alreadySent)
                {
                    continue;
                }

                await _notificationService.SendUnassignedReminderAsync(booking.Id, urgent);
            }
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~ReminderEscalationJobTests`
Expected: PASS (3 tests)

- [ ] **Step 10: Write and implement NoShowDetectionJob**

```csharp
// Tests/Jobs/NoShowDetectionJobTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideBooking.Data;
using RideBooking.Jobs;
using RideBooking.Models;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Jobs
{
    public class NoShowDetectionJobTests
    {
        private RideBookingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideBookingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideBookingDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideBookingDbContext context, DateTime pickupAtUtc, string status)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TEST0007",
                CustomerId = customer.Id,
                PickupLocation = "KL Sentral",
                Destination = "KLIA Terminal 1",
                PickupDate = DateOnly.FromDateTime(pickupAtUtc),
                PickupTime = TimeOnly.FromDateTime(pickupAtUtc),
                Passengers = 1,
                Bags = 0,
                RequestedVehicleType = "Car",
                Status = status
            };
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private INotificationService BuildNotificationService(RideBookingDbContext context) =>
            new NotificationService(context, new RideBooking.Tests.Services.FakeEmailSender(), new RideBooking.Tests.Services.FakeWhatsAppSender(),
                new RideBooking.Tests.Services.FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@ridebooking.my", SenderName = "RideBooking", OperatorEmail = "operator@ridebooking.my" }));

        [Fact]
        public async Task RunAsync_WithBookingNotPickedUp40MinutesAfterPickupTime_MarksAsNoShow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Driver_Assigned");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("No_Show", updated!.Status);
            var history = await context.BookingStatusHistories.FirstOrDefaultAsync(h => h.BookingId == booking.Id);
            Assert.NotNull(history);
            Assert.Equal("No_Show", history!.NewStatus);
        }

        [Fact]
        public async Task RunAsync_WithBookingAlreadyPickedUp_DoesNotChangeStatus()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-40), "Picked_Up");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Picked_Up", updated!.Status);
        }

        [Fact]
        public async Task RunAsync_WithinTheThirtyMinuteGracePeriod_DoesNotMarkAsNoShow()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context, DateTime.UtcNow.AddMinutes(-10), "Driver_Assigned");
            var job = new NoShowDetectionJob(context, BuildNotificationService(context));

            // Act
            await job.RunAsync();

            // Assert
            var updated = await context.Bookings.FindAsync(booking.Id);
            Assert.Equal("Driver_Assigned", updated!.Status);
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~NoShowDetectionJobTests`
Expected: FAIL (`NoShowDetectionJob` does not exist)

```csharp
// Jobs/NoShowDetectionJob.cs
using Microsoft.EntityFrameworkCore;
using Quartz;
using RideBooking.Data;
using RideBooking.Models;
using RideBooking.Services;

namespace RideBooking.Jobs
{
    public class NoShowDetectionJob : IJob
    {
        private static readonly string[] EligibleStatuses = { "New", "Confirmed", "Driver_Assigned" };
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

        private readonly RideBookingDbContext _context;
        private readonly INotificationService _notificationService;

        public NoShowDetectionJob(RideBookingDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task Execute(IJobExecutionContext context) => await RunAsync();

        internal async Task RunAsync()
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.Bookings
                .Where(b => EligibleStatuses.Contains(b.Status))
                .ToListAsync();

            foreach (var booking in candidates)
            {
                var pickupAt = booking.PickupDate.ToDateTime(booking.PickupTime);
                if (now - pickupAt < GracePeriod)
                {
                    continue;
                }

                var previousStatus = booking.Status;
                booking.Status = "No_Show";
                booking.UpdatedAt = now;

                _context.BookingStatusHistories.Add(new BookingStatusHistory
                {
                    BookingId = booking.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = "No_Show",
                    ChangedBy = "System"
                });

                await _context.SaveChangesAsync();
                await _notificationService.SendNoShowNotificationAsync(booking.Id);
            }
        }
    }
}
```

Run: `dotnet test --filter FullyQualifiedName~NoShowDetectionJobTests`
Expected: PASS (3 tests)

- [ ] **Step 11: Register the jobs with Quartz in Program.cs**

```csharp
builder.Services.AddQuartz(q =>
{
    var retryKey = new JobKey("NotificationRetryJob");
    q.AddJob<NotificationRetryJob>(opts => opts.WithIdentity(retryKey));
    q.AddTrigger(opts => opts
        .ForJob(retryKey)
        .WithIdentity("NotificationRetryJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var reminderKey = new JobKey("ReminderEscalationJob");
    q.AddJob<ReminderEscalationJob>(opts => opts.WithIdentity(reminderKey));
    q.AddTrigger(opts => opts
        .ForJob(reminderKey)
        .WithIdentity("ReminderEscalationJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

    var noShowKey = new JobKey("NoShowDetectionJob");
    q.AddJob<NoShowDetectionJob>(opts => opts.WithIdentity(noShowKey));
    q.AddTrigger(opts => opts
        .ForJob(noShowKey)
        .WithIdentity("NoShowDetectionJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
});
builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);
```

Add the corresponding `using` at the top of `Program.cs`:

```csharp
using RideBooking.Jobs;
```

- [ ] **Step 12: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 13: Commit**

```bash
git add Models/Notification.cs Migrations/ Services/ Jobs/ Tests/ Program.cs
git commit -m "feat: add Quartz background jobs for notification retries, reminders, and no-show detection"
```

---

## Part 7: CI/CD & Deployment

### Task 10: CI/CD Pipeline (GitHub Actions, Semantic Versioning)

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`
- Create: `package.json`
- Create: `.releaserc.json`

**Interfaces:**
- Produces: a CI workflow (build + test on every PR/push) and a release workflow (semantic-release + versioned Docker image pushed to GHCR)

> CI/CD config isn't unit-testable the way application code is — each step below is verified by pushing to a branch and observing the Actions tab, not `dotnet test`.

- [ ] **Step 1: Create the CI workflow**

```yaml
# .github/workflows/ci.yml
name: CI

on:
  pull_request:
  push:
    branches: [develop, uat, staging, main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore RideBooking/RideBooking.csproj

      - name: Build
        run: dotnet build RideBooking/RideBooking.csproj --configuration Release --no-restore

      - name: Test
        run: dotnet test RideBooking/RideBooking.csproj --configuration Release --no-build --verbosity normal
```

- [ ] **Step 2: Create the semantic-release config**

```json
// package.json
{
  "name": "ridebooking-release",
  "private": true,
  "version": "0.0.0-development"
}
```

```json
// .releaserc.json
{
  "branches": [
    "main",
    { "name": "staging", "channel": "staging", "prerelease": "staging" },
    { "name": "uat", "channel": "uat", "prerelease": "uat" },
    { "name": "develop", "channel": "develop", "prerelease": "develop" }
  ],
  "plugins": [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator",
    "@semantic-release/github"
  ]
}
```

- [ ] **Step 3: Create the release workflow**

```yaml
# .github/workflows/release.yml
name: Release

on:
  push:
    branches: [develop, uat, staging, main]

permissions:
  contents: write
  packages: write
  issues: write
  pull-requests: write

jobs:
  release:
    runs-on: ubuntu-latest
    outputs:
      new_release_version: ${{ steps.semantic.outputs.new_release_version }}
      new_release_published: ${{ steps.semantic.outputs.new_release_published }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Semantic Release
        id: semantic
        uses: cycjimmy/semantic-release-action@v4
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  docker:
    needs: release
    if: needs.release.outputs.new_release_published == 'true'
    runs-on: ubuntu-latest
    permissions:
      packages: write
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-buildx-action@v3

      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push versioned + branch tags
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: |
            ghcr.io/${{ github.repository }}:${{ needs.release.outputs.new_release_version }}
            ghcr.io/${{ github.repository }}:${{ github.ref_name }}

      - name: Also tag latest (main only)
        if: github.ref_name == 'main'
        run: docker buildx imagetools create -t ghcr.io/${{ github.repository }}:latest ghcr.io/${{ github.repository }}:${{ github.ref_name }}
```

- [ ] **Step 4: Verify the workflows are picked up**

Push this branch and open the repository's Actions tab.
Expected: the `CI` workflow run appears and its `build-and-test` job passes (green check).

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/ci.yml .github/workflows/release.yml package.json .releaserc.json
git commit -m "chore: add CI build/test workflow and semantic-release pipeline"
```

---

### Task 11: Docker & DigitalOcean Deployment

**Files:**
- Modify: `Program.cs`
- Create: `docker-compose.yml`
- Create: `scripts/deploy.sh`
- Create: `.github/workflows/deploy.yml`

**Interfaces:**
- Consumes: the Docker image published by Task 10's release workflow (`ghcr.io/<repo>:<tag>`)
- Produces: a droplet-based deployment (app + PostgreSQL containers) with an automated SSH deploy step

> **Scope note:** the spec (§9.3) calls for zero-downtime blue-green production deploys. Implementing true blue-green (dual containers behind a reverse proxy with a traffic-switch step) is a meaningfully larger infrastructure project than fits one task here, and isn't justified by Phase 1's expected traffic. This task implements an in-place restart gated by a post-deploy health check (deploy fails loudly if the new container doesn't become healthy); true blue-green is deferred to Phase 2.

- [ ] **Step 1: Add a health check endpoint and automatic migrations on startup**

The `Dockerfile` (from Task 1) already declares `HEALTHCHECK ... CMD curl -f http://localhost:5000/health`, but no `/health` endpoint exists yet. Add one, and apply pending EF Core migrations automatically when the app starts (so the deploy script doesn't need a separate migration step):

```csharp
// Program.cs
// Add near the top-level using statements:
using RideBooking.Jobs;
using RideBooking.Data;
```

```csharp
// After "var app = builder.Build();" and before "if (!app.Environment.IsDevelopment())":
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RideBookingDbContext>();
    await db.Database.MigrateAsync();
}
```

```csharp
// After "app.UseAuthorization();" and before "app.MapControllerRoute(...)":
app.MapHealthChecks("/health");
```

And register health checks alongside the other service registrations:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RideBookingDbContext>();
```

- [ ] **Step 2: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 errors

Run: `dotnet test`
Expected: PASS (all tests)

- [ ] **Step 3: Provision the DigitalOcean droplet (manual, one-time, per environment)**

This is infrastructure provisioning, not application code — run once per environment (develop/uat/staging/production) via the DigitalOcean console or `doctl`:

```bash
doctl compute droplet create ridebooking-production \
  --region sgp1 \
  --image docker-20-04 \
  --size s-2vcpu-4gb \
  --ssh-keys <your-ssh-key-fingerprint>
```

On the droplet: create the deploy directory, restrict inbound traffic to SSH/HTTP/HTTPS in the DigitalOcean firewall, and enable daily backups from the DigitalOcean console (per spec §15.1, 30-day retention).

```bash
ssh root@<droplet-ip> "mkdir -p /opt/ridebooking/scripts"
```

- [ ] **Step 4: Create the droplet's docker-compose file**

```yaml
# docker-compose.yml
services:
  app:
    image: ${IMAGE_NAME:-ghcr.io/OWNER/REPO}:${IMAGE_TAG:-latest}
    restart: unless-stopped
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Host=db;Database=ride_booking;Username=${DB_USER};Password=${DB_PASSWORD}"
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  db:
    image: postgres:15
    restart: unless-stopped
    environment:
      POSTGRES_DB: ride_booking
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

`DB_USER`, `DB_PASSWORD`, and `IMAGE_NAME` are provided on the droplet via a gitignored `.env` file next to this compose file, not committed to the repo.

- [ ] **Step 5: Create the deploy script**

```bash
#!/usr/bin/env bash
# scripts/deploy.sh
set -euo pipefail

IMAGE_TAG="${1:?Usage: deploy.sh <image-tag>}"
export IMAGE_TAG

docker compose pull app
docker compose up -d
docker compose ps

echo "Waiting for health check..."
for i in $(seq 1 10); do
  if curl -sf http://localhost:5000/health > /dev/null; then
    echo "Healthy."
    exit 0
  fi
  sleep 5
done

echo "Health check failed after deploy. The previous image is still available locally —" >&2
echo "roll back manually with: IMAGE_TAG=<previous-tag> docker compose up -d" >&2
exit 1
```

- [ ] **Step 6: Create the deploy workflow**

Triggered after Task 10's `Release` workflow succeeds on a deployable branch:

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  workflow_run:
    workflows: ["Release"]
    types: [completed]
    branches: [develop, uat, staging, main]

jobs:
  deploy:
    if: github.event.workflow_run.conclusion == 'success'
    runs-on: ubuntu-latest
    environment: ${{ github.event.workflow_run.head_branch == 'main' && 'production' || github.event.workflow_run.head_branch }}
    steps:
      - uses: actions/checkout@v4

      - name: Copy compose files to droplet
        uses: appleboy/scp-action@v0.1.7
        with:
          host: ${{ secrets.DROPLET_HOST }}
          username: ${{ secrets.DROPLET_USER }}
          key: ${{ secrets.DROPLET_SSH_KEY }}
          source: "docker-compose.yml,scripts/deploy.sh"
          target: "/opt/ridebooking"

      - name: Run deploy script over SSH
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.DROPLET_HOST }}
          username: ${{ secrets.DROPLET_USER }}
          key: ${{ secrets.DROPLET_SSH_KEY }}
          script: |
            cd /opt/ridebooking
            chmod +x scripts/deploy.sh
            IMAGE_NAME=ghcr.io/${{ github.repository }} ./scripts/deploy.sh ${{ github.event.workflow_run.head_branch }}
```

- [ ] **Step 7: Configure GitHub Environments for manual approval (one-time repo setting)**

Per spec §9.3, `staging` and `production` deploys require manual approval; `develop` and `uat` auto-deploy. In the repository's Settings → Environments:
1. Create environments named `develop`, `uat`, `staging`, `production`.
2. On `staging` and `production`, add a required reviewer under "Deployment protection rules" — this pauses the `deploy` job until approved.
3. Add `DROPLET_HOST`, `DROPLET_USER`, and `DROPLET_SSH_KEY` as environment secrets on each of the four environments, pointing at that environment's droplet.

This is a manual, one-time repository configuration step (not expressible in the workflow YAML itself).

- [ ] **Step 8: Document the rollback procedure**

If a deploy's health check fails (Step 5's script exits non-zero), the running container is left on the previous image (`docker compose up -d` on a failed pull leaves the last-known-good container running). To roll back an already-succeeded-but-bad deploy, SSH into the droplet and run:

```bash
cd /opt/ridebooking
IMAGE_TAG=<previous-known-good-tag> docker compose up -d
```

- [ ] **Step 9: Commit**

```bash
git add Program.cs docker-compose.yml scripts/deploy.sh .github/workflows/deploy.yml
git commit -m "feat: add health check, auto-migrate on startup, and DigitalOcean deploy pipeline"
```

---

## Plan Complete

All 11 tasks are now fully specified. Tasks 1-3 are implemented and committed; Tasks 4-11 are ready for execution.

**Self-review notes:**
- **Spec coverage:** every numbered section of the design spec (§1-§20) maps to at least one task — customer form (§5) → Task 4; pricing (§6, admin-only) → Tasks 3-6; notifications (§7) → Task 8; location (§8) → Tasks 5, 7; CI/CD (§9) → Task 10; error handling/no-show/escalation (§10) → Task 9; security/auth (§11) → Tasks 6-7; UI reference (§20) → Tasks 4, 6, 7. Analytics/reporting (§3.2), live driver map, full Google Calendar OAuth, and true blue-green deploys are explicitly scoped out of Phase 1 with reasons noted in the relevant task.
- **Type consistency:** `AcceptAssignmentAsync`'s signature change (Task 7 → Task 8) and `SendAndLogAsync`'s signature change (Task 8 → Task 9) are both called out explicitly with old/new code and the corresponding test updates, so later tasks don't reference a stale signature.
- **No placeholders:** every step includes complete, runnable code or an exact command with expected output.
