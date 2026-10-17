using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideReady.Controllers;
using RideReady.Data;
using RideReady.Services;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class BookingMessageAndDefaultsTests
    {
        private static readonly string[] FrameworkPhrases =
        {
            "The field", "must be a string", "minimum length", "maximum length", "regular expression",
            "is not a valid", "e-mail", "must be between"
        };

        private static BookingRequestViewModel Valid() => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "012-3456789",
            CustomerEmail = "sim@email.com",
            FromName = "KLIA T1",
            ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
            Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = new DateOnly(2026, 10, 20),
            PickupTime = new TimeOnly(12, 0),
            DropOffDate = new DateOnly(2026, 10, 20),
            DropOffTime = new TimeOnly(15, 0),
            Passengers = 2,
            Bags = 1,
            VehicleType = "Car",
            PaymentMethod = "Pay_at_Pickup",
            AcceptedTerms = true
        };

        private static string? MessageFor(BookingRequestViewModel model, string member)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.FirstOrDefault(r => r.MemberNames.Contains(member))?.ErrorMessage;
        }

        // ---- every rule has a message written for people ----

        [Fact]
        public void EveryValidationRuleHasAnExplicitFriendlyMessage()
        {
            var attributes = typeof(BookingRequestViewModel).GetProperties()
                .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>(), (p, a) => (Property: p.Name, Attribute: a))
                .ToList();

            Assert.NotEmpty(attributes);
            foreach (var (property, attribute) in attributes)
            {
                // A null ErrorMessage means the framework's own technical wording would be shown.
                Assert.False(string.IsNullOrWhiteSpace(attribute.ErrorMessage),
                    $"{property} has a {attribute.GetType().Name} without its own message");
                foreach (var phrase in FrameworkPhrases)
                {
                    Assert.DoesNotContain(phrase, attribute.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void MessagesAreWrittenAsFullSentences()
        {
            foreach (var attribute in typeof(BookingRequestViewModel).GetProperties()
                         .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>()))
            {
                Assert.EndsWith(".", attribute.ErrorMessage!);
                Assert.True(char.IsUpper(attribute.ErrorMessage![0]), attribute.ErrorMessage);
            }
        }

        // ---- examples a customer would hit ----

        [Theory]
        [InlineData(nameof(BookingRequestViewModel.CustomerName), "Al", "Please enter your full name, between 3 and 100 characters.")]
        [InlineData(nameof(BookingRequestViewModel.CustomerName), "", "Please enter your full name.")]
        [InlineData(nameof(BookingRequestViewModel.CustomerPhone), "abc", "Enter a Malaysian phone number, like 012-3456789.")]
        [InlineData(nameof(BookingRequestViewModel.CustomerPhone), "", "Please enter your phone number.")]
        [InlineData(nameof(BookingRequestViewModel.CustomerEmail), "lito@", "Enter a full email address, like you@example.com.")]
        [InlineData(nameof(BookingRequestViewModel.CustomerEmail), "", "Please enter your email address.")]
        [InlineData(nameof(BookingRequestViewModel.FromName), "", "Please enter where you are starting from.")]
        [InlineData(nameof(BookingRequestViewModel.ToName), "", "Please enter where you are going to.")]
        [InlineData(nameof(BookingRequestViewModel.PickupLocation), "", "Please enter the pick up address.")]
        [InlineData(nameof(BookingRequestViewModel.Destination), "abc", "Please use between 5 and 255 characters.")]
        [InlineData(nameof(BookingRequestViewModel.VehicleType), "", "Please choose a vehicle type.")]
        [InlineData(nameof(BookingRequestViewModel.VehicleType), "Helicopter", "Please choose Car, Van or Bus.")]
        [InlineData(nameof(BookingRequestViewModel.PaymentMethod), "Cash", "Please choose Pay at Pickup or Bank Transfer.")]
        public void TextFieldsGiveAFriendlyMessage(string member, string value, string expected)
        {
            var model = Valid();
            typeof(BookingRequestViewModel).GetProperty(member)!.SetValue(model, value);

            Assert.Equal(expected, MessageFor(model, member));
        }

        [Fact]
        public void PassengersAndBags_GiveFriendlyRangeMessages()
        {
            var tooFew = Valid();
            tooFew.Passengers = 0;
            var tooMany = Valid();
            tooMany.Passengers = 101;
            var bags = Valid();
            bags.Bags = 11;

            Assert.Equal("Please enter between 1 and 100 passengers.", MessageFor(tooFew, "Passengers"));
            Assert.Equal("Please enter between 1 and 100 passengers.", MessageFor(tooMany, "Passengers"));
            Assert.Equal("Please enter between 0 and 10 bags.", MessageFor(bags, "Bags"));
        }

        [Fact]
        public void MissingDatesAndTimes_GiveFriendlyMessages()
        {
            var model = Valid();
            model.DropOffDate = null;
            model.DropOffTime = null;

            Assert.Equal("Please choose a drop-off date.", MessageFor(model, "DropOffDate"));
            Assert.Equal("Please choose an approximate drop-off time.", MessageFor(model, "DropOffTime"));
        }

        [Fact]
        public void DropOffBeforePickup_SaysSo()
        {
            var model = Valid();
            model.DropOffTime = new TimeOnly(11, 0);

            Assert.Equal("The drop-off time must be after the pickup time.", MessageFor(model, "DropOffTime"));
        }

        [Fact]
        public void UntickedTerms_AsksPolitely()
        {
            var model = Valid();
            model.AcceptedTerms = false;

            Assert.Equal("Please accept the terms and conditions to continue.", MessageFor(model, "AcceptedTerms"));
        }

        [Fact]
        public void RemarksTooLong_SaysSoPlainly()
        {
            var model = Valid();
            model.Notes = new string('x', 501);

            Assert.Equal("Please keep remarks under 500 characters.", MessageFor(model, "Notes"));
        }

        // ---- service messages use the same voice ----

        [Fact]
        public async Task ServiceMessages_AreFriendly()
        {
            var context = new RideReadyDbContext(new DbContextOptionsBuilder<RideReadyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            var service = new BookingService(context);

            var past = Valid();
            past.PickupDate = new DateOnly(2020, 1, 1);
            var pastEx = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingAsync(past));
            Assert.Equal("Please choose a pickup time in the future.", pastEx.Message);

            var night = Valid();
            night.PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
            night.PickupTime = new TimeOnly(3, 0);
            var nightEx = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBookingAsync(night));
            Assert.Equal("Bookings are available from 6:00 AM to midnight.", nightEx.Message);
        }

        // ---- sensible defaults ----

        [Fact]
        public void NewForm_StartsWithACarOnePassengerAndTodaysDates()
        {
            var today = new DateOnly(2026, 10, 20);

            var form = BookingRequestViewModel.NewForm(today);

            Assert.Equal("Car", form.VehicleType);
            Assert.Equal(1, form.Passengers);
            Assert.Equal(today, form.PickupDate);
            Assert.Equal(today, form.DropOffDate);
            Assert.Equal(0, form.Bags);
            Assert.Equal("Pay_at_Pickup", form.PaymentMethod);
            Assert.False(form.AcceptedTerms);
        }

        private static RideReadyDbContext NewContext() => new(new DbContextOptionsBuilder<RideReadyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task BookingPage_OpensWithTheDefaults()
        {
            var controller = new BookingController(new BookingService(NewContext()), null!);

            var model = Assert.IsType<BookingRequestViewModel>(Assert.IsType<ViewResult>(await controller.Create()).Model);

            Assert.Equal("Car", model.VehicleType);
            Assert.Equal(1, model.Passengers);
        }

        [Fact]
        public async Task FrontPage_OpensWithTheSameDefaults()
        {
            var controller = new HomeController(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeController>.Instance, new BookingService(NewContext()));

            var model = Assert.IsType<BookingRequestViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);

            Assert.Equal("Car", model.VehicleType);
            Assert.Equal(1, model.Passengers);
        }
    }
}
