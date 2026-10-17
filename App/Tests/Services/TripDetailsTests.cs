using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.Services
{
    /// <summary>From/To names, pick up and drop-off addresses, and the approximate drop-off date and time.</summary>
    public class TripDetailsTests
    {
        private static RideReadyDbContext NewContext() =>
            new(new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        private static BookingRequestViewModel Request() => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            CustomerEmail = "sim@email.com",
            FromName = "KLIA T1",
            ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
            Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            PickupTime = new TimeOnly(12, 0),
            DropOffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            DropOffTime = new TimeOnly(15, 0),
            Passengers = 2,
            Bags = 1,
            VehicleType = "Car",
            Notes = "Flight MH123",
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        private static async Task<Booking> SeedBookingAsync(RideReadyDbContext context, Action<Booking>? customise = null)
        {
            var customer = new Customer { Name = "Uncle Sim", Phone = "0125183838", Email = "sim@email.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var booking = new Booking
            {
                BookingReference = "RR-TRIP0001",
                CustomerId = customer.Id,
                PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
                Destination = "Mid Valley Megamall, Kuala Lumpur",
                FromName = "KLIA T1",
                ToName = "Mid Valley",
                PickupDate = new DateOnly(2026, 10, 20),
                PickupTime = new TimeOnly(12, 0),
                DropOffDate = new DateOnly(2026, 10, 20),
                DropOffTime = new TimeOnly(15, 0),
                Passengers = 2,
                Bags = 1,
                RequestedVehicleType = "Car",
                Status = "New"
            };
            customise?.Invoke(booking);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            return booking;
        }

        private static NotificationService NewNotifier(RideReadyDbContext context, FakeWhatsAppSender whatsApp) =>
            new(context, new FakeEmailSender(), whatsApp, new FakeCalendarSyncService(),
                Options.Create(new EmailSettings { SenderEmail = "noreply@rideready.my", SenderName = "RideReady", OperatorEmail = "operator@rideready.my" }),
                Options.Create(new WhatsAppSettings { ApiUrl = "https://graph.facebook.com/v18.0", AccessToken = "t", PhoneNumberId = "1", OperatorPhone = "0192462592" }));

        // ---- saving the booking ----

        [Fact]
        public async Task CreateBooking_SavesTheNewTripFields()
        {
            var context = NewContext();
            var request = Request();

            var booking = await new BookingService(context).CreateBookingAsync(request);

            var saved = await context.Bookings.SingleAsync(b => b.Id == booking.Id);
            Assert.Equal("KLIA T1", saved.FromName);
            Assert.Equal("Mid Valley", saved.ToName);
            Assert.Equal("Level 3, KLIA Terminal 1, Sepang", saved.PickupLocation);
            Assert.Equal("Mid Valley Megamall, Kuala Lumpur", saved.Destination);
            Assert.Equal(request.DropOffDate, saved.DropOffDate);
            Assert.Equal(new TimeOnly(15, 0), saved.DropOffTime);
            Assert.Equal("Flight MH123", saved.Notes);
        }

        [Fact]
        public async Task CreateBooking_WithOptionalFieldsBlank_StillSucceedsAndStoresNulls()
        {
            var context = NewContext();
            var request = Request();
            request.FromName = "  ";
            request.ToName = null;
            request.PickupLocation = null;

            var booking = await new BookingService(context).CreateBookingAsync(request);

            var saved = await context.Bookings.SingleAsync(b => b.Id == booking.Id);
            Assert.Null(saved.FromName);
            Assert.Null(saved.ToName);
            Assert.Equal(string.Empty, saved.PickupLocation);
            Assert.NotNull(saved.Quote ?? await context.BookingQuotes.FirstOrDefaultAsync(q => q.BookingId == saved.Id));
        }

        [Fact]
        public async Task CreateBooking_WhenDropOffIsNotAfterPickup_Throws()
        {
            var request = Request();
            request.DropOffDate = request.PickupDate;
            request.DropOffTime = request.PickupTime;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new BookingService(NewContext()).CreateBookingAsync(request));

            Assert.Contains("after the pickup", ex.Message);
        }

        [Fact]
        public async Task CreateBooking_WithoutDropOffDateAndTime_Throws()
        {
            var request = Request();
            request.DropOffDate = null;
            request.DropOffTime = null;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new BookingService(NewContext()).CreateBookingAsync(request));
        }

        // ---- messages ----

        [Fact]
        public async Task OperatorNewBookingMessage_IncludesRouteAddressesAndApproximateDropOff()
        {
            var context = NewContext();
            var booking = await SeedBookingAsync(context);
            var whatsApp = new FakeWhatsAppSender();

            await NewNotifier(context, whatsApp).SendBookingCreatedNotificationAsync(booking.Id);

            var operatorMessage = whatsApp.Sent.Single(s => s.To == "0192462592").Message;
            Assert.Contains("RR-TRIP0001", operatorMessage);
            Assert.Contains("KLIA T1 -> Mid Valley", operatorMessage);
            Assert.Contains("Pick up: Level 3, KLIA Terminal 1, Sepang on 2026-10-20 at 12:00", operatorMessage);
            Assert.Contains("Drop-off: Mid Valley Megamall, Kuala Lumpur, approx. 2026-10-20 15:00", operatorMessage);
        }

        [Fact]
        public async Task DriverNewJobMessage_IncludesTheSameTripDetails()
        {
            var context = NewContext();
            var booking = await SeedBookingAsync(context);
            var driver = new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "TEST 1234", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            var whatsApp = new FakeWhatsAppSender();

            await NewNotifier(context, whatsApp).SendDriverAssignedNotificationAsync(booking.Id, driver.Id);

            var message = whatsApp.Sent.Single(s => s.To == "0167237377").Message;
            Assert.Contains("KLIA T1 -> Mid Valley", message);
            Assert.Contains("Pick up: Level 3, KLIA Terminal 1, Sepang", message);
            Assert.Contains("approx. 2026-10-20 15:00", message);
            Assert.EndsWith("Log in to the Driver Portal to accept or reject.", message);
        }

        [Fact]
        public async Task Messages_WithoutShortNamesOrPickupAddress_FallBackCleanly()
        {
            var context = NewContext();
            var booking = await SeedBookingAsync(context, b =>
            {
                b.FromName = null;
                b.ToName = null;
                b.PickupLocation = string.Empty;
                b.DropOffDate = null;
                b.DropOffTime = null;
            });
            var whatsApp = new FakeWhatsAppSender();

            await NewNotifier(context, whatsApp).SendBookingCreatedNotificationAsync(booking.Id);

            var message = whatsApp.Sent.Single(s => s.To == "0192462592").Message;
            Assert.Contains("- -> Mid Valley Megamall, Kuala Lumpur", message);
            Assert.Contains("Pick up on 2026-10-20 at 12:00", message);
            Assert.DoesNotContain("approx.", message);
            Assert.DoesNotContain("Pick up:", message);
        }

        // ---- dashboard / driver portal data ----

        [Fact]
        public async Task Dashboard_CarriesEmailRemarksAndPayment_ButDriverPortalDoesNotExposeThem()
        {
            var context = NewContext();
            var booking = await SeedBookingAsync(context, b => b.Notes = "Child seat please");
            context.BookingQuotes.Add(new BookingQuote { BookingId = booking.Id, TotalEstimatedFare = 150m, PaymentMethod = "Bank_Transfer" });
            await context.SaveChangesAsync();
            var assignments = new DriverAssignmentService(context);
            var driver = await assignments.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "TEST 1234", Pin = "7737"
            });
            await assignments.AssignDriverAsync(booking.Id, driver.Id);

            var adminItem = Assert.Single(await assignments.GetDashboardBookingsAsync());
            Assert.Equal("sim@email.com", adminItem.CustomerEmail);
            Assert.Equal("Child seat please", adminItem.Notes);
            Assert.Equal("Bank_Transfer", adminItem.PaymentMethod);
            Assert.Equal(150m, adminItem.EstimatedFare);

            var driverTrip = Assert.Single(await new DriverPortalService(context).GetAssignmentsAsync(driver.Id));
            Assert.Equal("Child seat please", driverTrip.Notes);
            Assert.Equal("Car", driverTrip.RequestedVehicleType);
            var driverRows = TripDetailsViewModel.ForDriver(driverTrip).Rows();
            Assert.DoesNotContain(driverRows, r => r.Value.Contains("sim@email.com") || r.Value.Contains("150") || r.Value.Contains("Transfer"));
        }

        [Fact]
        public async Task DashboardAndDriverPortal_CarryTheNewFields()
        {
            var context = NewContext();
            var booking = await SeedBookingAsync(context);
            var assignments = new DriverAssignmentService(context);
            var driver = await assignments.CreateDriverAsync(new CreateDriverViewModel
            {
                Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "TEST 1234", Pin = "7737"
            });
            await assignments.AssignDriverAsync(booking.Id, driver.Id);

            var dashboardItem = Assert.Single(await assignments.GetDashboardBookingsAsync());
            Assert.Equal("KLIA T1", dashboardItem.FromName);
            Assert.Equal("Mid Valley", dashboardItem.ToName);
            Assert.Equal(new DateOnly(2026, 10, 20), dashboardItem.DropOffDate);
            Assert.Equal(new TimeOnly(15, 0), dashboardItem.DropOffTime);

            var driverTrip = Assert.Single(await new DriverPortalService(context).GetAssignmentsAsync(driver.Id));
            Assert.Equal("KLIA T1", driverTrip.FromName);
            Assert.Equal("Mid Valley", driverTrip.ToName);
            Assert.Equal(new TimeOnly(15, 0), driverTrip.DropOffTime);
        }
    }
}
