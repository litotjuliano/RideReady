using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideReady.Data;
using RideReady.Models;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
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
        private RideReadyDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new RideReadyDbContext(options);
        }

        private async Task<Booking> SeedBookingAsync(RideReadyDbContext context)
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
            SenderEmail = "noreply@rideready.my",
            SenderName = "RideReady",
            OperatorEmail = "operator@rideready.my"
        });

        private static IOptions<WhatsAppSettings> WhatsAppOptions() => Options.Create(new WhatsAppSettings
        {
            ApiUrl = "https://graph.facebook.com/v18.0",
            AccessToken = "test-token",
            PhoneNumberId = "1234567890",
            OperatorPhone = "0192462592"
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
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings(), WhatsAppOptions());

            // Act
            await service.SendBookingCreatedNotificationAsync(booking.Id);

            // Assert
            Assert.Equal(2, emailSender.Sent.Count);
            Assert.Contains(emailSender.Sent, s => s.To == "sim@email.com");
            Assert.Contains(emailSender.Sent, s => s.To == "operator@rideready.my");
            Assert.Equal(2, whatsAppSender.Sent.Count);
            Assert.Contains(whatsAppSender.Sent, s => s.To == "0125183838");
            Assert.Contains(whatsAppSender.Sent, s => s.To == "0192462592");
            Assert.Equal(1, calendarSync.CallCount);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(5, notifications.Count);
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
            var service = new NotificationService(context, emailSender, whatsAppSender, calendarSync, Settings(), WhatsAppOptions());

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
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendDriverAssignedNotificationAsync(booking.Id, driver.Id);

            // Assert
            Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0123456789", whatsAppSender.Sent[0].To);
            Assert.Single(emailSender.Sent);
        }

        [Fact]
        public async Task SendBookingCancelledNotificationAsync_WithDriverAssigned_SendsCustomerEmailAndDriverWhatsApp()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            context.DriverAssignments.Add(new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = "Accepted"
            });
            await context.SaveChangesAsync();
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendBookingCancelledNotificationAsync(booking.Id);

            // Assert
            Assert.Single(emailSender.Sent);
            Assert.Equal("sim@email.com", emailSender.Sent[0].To);
            Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0123456789", whatsAppSender.Sent[0].To);
            var notifications = await context.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
            Assert.Equal(2, notifications.Count);
        }

        [Fact]
        public async Task SendBookingCancelledNotificationAsync_WithNoDriverAssigned_SendsOnlyCustomerEmail()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendBookingCancelledNotificationAsync(booking.Id);

            // Assert (no exception thrown)
            Assert.Single(emailSender.Sent);
            Assert.Equal("sim@email.com", emailSender.Sent[0].To);
            Assert.Empty(whatsAppSender.Sent);
        }

        [Fact]
        public async Task SendBookingCancelledNotificationAsync_WhenOnlyAssignmentWasRejected_SendsOnlyCustomerEmail()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var driver = new Driver { Name = "Ah Seng", Phone = "0123456789", VehicleType = "Car", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            context.DriverAssignments.Add(new DriverAssignment
            {
                BookingId = booking.Id,
                DriverId = driver.Id,
                AssignmentStatus = "Rejected"
            });
            await context.SaveChangesAsync();
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendBookingCancelledNotificationAsync(booking.Id);

            // Assert
            Assert.Single(emailSender.Sent);
            Assert.Empty(whatsAppSender.Sent);
        }

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

        [Fact]
        public async Task SendDriverAcceptedNotificationAsync_NotifiesOnlyTheOperator_NotTheCustomer()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendDriverAcceptedNotificationAsync(booking.Id);

            // Assert: the customer only hears from us when the admin sends the driver details manually
            Assert.Empty(emailSender.Sent);
            var sent = Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0192462592", sent.To);
            Assert.Contains(booking.BookingReference, sent.Message);
        }

        private async Task<Driver> SeedAcceptedDriverAsync(RideReadyDbContext context, Booking booking, string assignmentStatus = "Accepted")
        {
            var driver = new Driver { Name = "Richard", Phone = "0167237377", VehicleType = "Car", VehicleNumber = "TEST 1234", PinHash = "x" };
            context.Drivers.Add(driver);
            await context.SaveChangesAsync();
            context.DriverAssignments.Add(new DriverAssignment { BookingId = booking.Id, DriverId = driver.Id, AssignmentStatus = assignmentStatus });
            await context.SaveChangesAsync();
            return driver;
        }

        [Fact]
        public async Task SendDriverDetailsToCustomerAsync_SendsDriverNamePhoneAndVehicleToCustomerByEmailAndWhatsApp()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            await SeedAcceptedDriverAsync(context, booking);
            var emailSender = new FakeEmailSender();
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            var sent = await service.SendDriverDetailsToCustomerAsync(booking.Id);

            // Assert
            Assert.True(sent);
            var whatsApp = Assert.Single(whatsAppSender.Sent);
            Assert.Equal("0125183838", whatsApp.To);
            Assert.Contains("Richard", whatsApp.Message);
            Assert.Contains("0167237377", whatsApp.Message);
            Assert.Contains("Car TEST 1234", whatsApp.Message);
            Assert.Contains(booking.BookingReference, whatsApp.Message);
            var email = Assert.Single(emailSender.Sent);
            Assert.Equal("sim@email.com", email.To);
            Assert.True(await context.Notifications.AnyAsync(n =>
                n.BookingId == booking.Id && n.EventType == NotificationService.DriverDetailsEvent && n.Channel == "WhatsApp" && n.DeliveryStatus == "Sent"));
        }

        [Fact]
        public async Task SendDriverDetailsToCustomerAsync_WhenFareIsSet_IncludesTheFareAndPaymentMethod()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            await SeedAcceptedDriverAsync(context, booking);
            context.BookingQuotes.Add(new BookingQuote
            {
                BookingId = booking.Id,
                TotalEstimatedFare = 150m,
                PaymentMethod = "Bank_Transfer"
            });
            await context.SaveChangesAsync();
            var whatsAppSender = new FakeWhatsAppSender();
            var emailSender = new FakeEmailSender();
            var service = new NotificationService(context, emailSender, whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendDriverDetailsToCustomerAsync(booking.Id);

            // Assert
            Assert.Contains("Fare: RM150.00 (Bank Transfer).", Assert.Single(whatsAppSender.Sent).Message);
            Assert.Contains("Fare: RM150.00 (Bank Transfer).", Assert.Single(emailSender.Sent).Body);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SendDriverDetailsToCustomerAsync_WhenNoFareIsSet_SaysNothingAboutPrice(bool hasZeroFareQuote)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            await SeedAcceptedDriverAsync(context, booking);
            if (hasZeroFareQuote)
            {
                context.BookingQuotes.Add(new BookingQuote
                {
                    BookingId = booking.Id,
                    TotalEstimatedFare = 0,
                    PaymentMethod = "Pay_at_Pickup"
                });
                await context.SaveChangesAsync();
            }
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, new FakeEmailSender(), whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            await service.SendDriverDetailsToCustomerAsync(booking.Id);

            // Assert
            var message = Assert.Single(whatsAppSender.Sent).Message;
            Assert.DoesNotContain("RM", message);
            Assert.DoesNotContain("Fare", message);
            Assert.EndsWith("at 09:00.", message);
        }

        [Fact]
        public async Task SendDriverDetailsToCustomerAsync_WhenDriverHasNotAccepted_Throws()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            await SeedAcceptedDriverAsync(context, booking, assignmentStatus: "Pending");
            var whatsAppSender = new FakeWhatsAppSender();
            var service = new NotificationService(context, new FakeEmailSender(), whatsAppSender, new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendDriverDetailsToCustomerAsync(booking.Id));
            Assert.Empty(whatsAppSender.Sent);
        }

        [Fact]
        public async Task SendDriverDetailsToCustomerAsync_WhenWhatsAppFails_ReturnsFalseAndLogsFailure()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var booking = await SeedBookingAsync(context);
            await SeedAcceptedDriverAsync(context, booking);
            var service = new NotificationService(context, new FakeEmailSender(), new ThrowingWhatsAppSender(), new FakeCalendarSyncService(), Settings(), WhatsAppOptions());

            // Act
            var sent = await service.SendDriverDetailsToCustomerAsync(booking.Id);

            // Assert
            Assert.False(sent);
            Assert.True(await context.Notifications.AnyAsync(n =>
                n.EventType == NotificationService.DriverDetailsEvent && n.Channel == "WhatsApp" && n.DeliveryStatus == "Failed"));
        }

        private sealed class ThrowingWhatsAppSender : IWhatsAppSender
        {
            public Task SendAsync(string toPhone, string message) => throw new HttpRequestException("401 Unauthorized");
        }


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
    }
}
