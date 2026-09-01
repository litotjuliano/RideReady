# Ride Booking System Phase 1 (MVP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a functional vehicle booking platform with customer booking, admin management, driver portal, multi-channel notifications, and CI/CD pipeline.

**Architecture:** ASP.NET Core 8 MVC monolith with PostgreSQL, background workers for async notifications, event-driven architecture for loose coupling between services.

**Tech Stack:** ASP.NET Core 8, Entity Framework Core 8, PostgreSQL 15, Bootstrap 5, Google Maps API, WhatsApp Business API, Google Calendar API, Quartz.NET, Docker, GitHub Actions

## Global Constraints

- .NET 8.0 minimum (C# 12)
- PostgreSQL 15+
- All currency in Philippine Peso (₱)
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

Remaining tasks (continued in next section due to length):
- Task 4: Create customer booking form (MVC controller + Razor views)
- Task 5: Implement pricing service with Google Maps integration
- Task 6: Create admin dashboard (booking list, assignment UI)
- Task 7: Create driver portal
- Task 8: Implement notification service (email, WhatsApp, Google Calendar)
- Task 9: Set up background jobs (Quartz.NET for reminders)
- Task 10: Configure CI/CD pipeline
- Task 11: Docker & DigitalOcean deployment

**Save this file and continue with remaining tasks in next phase.**
