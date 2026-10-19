using System.ComponentModel.DataAnnotations;
using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class BookingRequestValidationTests
    {
        private static BookingRequestViewModel ValidRequest() => new()
        {
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
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
            AcceptedTerms = true
        };

        private static List<ValidationResult> Validate(BookingRequestViewModel model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        private static bool HasErrorFor(List<ValidationResult> results, string member) =>
            results.Any(r => r.MemberNames.Contains(member));

        [Fact]
        public void ValidRequest_HasNoErrors()
        {
            Assert.Empty(Validate(ValidRequest()));
        }

        [Fact]
        public void Remarks_AreTheOnlyOptionalField()
        {
            var model = ValidRequest();
            model.Notes = null;

            Assert.Empty(Validate(model));
        }

        [Theory]
        [InlineData("FromName")]
        [InlineData("ToName")]
        [InlineData("PickupLocation")]
        [InlineData("Destination")]
        public void TripFields_AreAllRequired(string member)
        {
            foreach (var blank in new string?[] { null, "", "   " })
            {
                var model = ValidRequest();
                typeof(BookingRequestViewModel).GetProperty(member)!.SetValue(model, blank);

                Assert.True(HasErrorFor(Validate(model), member), $"{member} = '{blank}' should be rejected");
            }
        }

        [Fact]
        public void DropOffAddress_IsRequired()
        {
            var model = ValidRequest();
            model.Destination = string.Empty;

            Assert.True(HasErrorFor(Validate(model), nameof(BookingRequestViewModel.Destination)));
        }

        [Fact]
        public void DropOffDate_IsRequired()
        {
            var model = ValidRequest();
            model.DropOffDate = null;

            Assert.True(HasErrorFor(Validate(model), nameof(BookingRequestViewModel.DropOffDate)));
        }

        [Fact]
        public void DropOffTime_IsRequired()
        {
            var model = ValidRequest();
            model.DropOffTime = null;

            Assert.True(HasErrorFor(Validate(model), nameof(BookingRequestViewModel.DropOffTime)));
        }

        [Theory]
        [InlineData("FromName")]
        [InlineData("ToName")]
        [InlineData("PickupLocation")]
        [InlineData("Destination")]
        public void TextFields_AreLimitedTo255Characters(string member)
        {
            var model = ValidRequest();
            typeof(BookingRequestViewModel).GetProperty(member)!.SetValue(model, new string('x', 256));

            Assert.True(HasErrorFor(Validate(model), member));
        }

        [Theory]
        [InlineData("FromName")]
        [InlineData("ToName")]
        [InlineData("PickupLocation")]
        [InlineData("Destination")]
        public void TextFields_Accept255Characters(string member)
        {
            var model = ValidRequest();
            typeof(BookingRequestViewModel).GetProperty(member)!.SetValue(model, new string('x', 255));

            Assert.False(HasErrorFor(Validate(model), member));
        }

        [Fact]
        public void DropOff_BeforeThePickup_IsRejected()
        {
            var model = ValidRequest();
            model.DropOffTime = new TimeOnly(11, 0); // same day, earlier than the 12:00 pickup

            var results = Validate(model);

            Assert.True(HasErrorFor(results, nameof(BookingRequestViewModel.DropOffTime)));
            Assert.Contains(results, r => r.ErrorMessage!.Contains("after the pickup"));
        }

        [Fact]
        public void DropOff_AtTheSameMomentAsThePickup_IsRejected()
        {
            var model = ValidRequest();
            model.DropOffTime = model.PickupTime;

            Assert.True(HasErrorFor(Validate(model), nameof(BookingRequestViewModel.DropOffTime)));
        }

        [Fact]
        public void DropOff_OnALaterDate_IsAcceptedEvenWithAnEarlierClockTime()
        {
            var model = ValidRequest();
            model.DropOffDate = new DateOnly(2026, 10, 21);
            model.DropOffTime = new TimeOnly(1, 0);

            Assert.Empty(Validate(model));
        }
    }
}
