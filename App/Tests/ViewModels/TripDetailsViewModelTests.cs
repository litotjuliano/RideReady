using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class TripDetailsViewModelTests
    {
        private static AdminBookingListItemViewModel AdminBooking() => new()
        {
            BookingId = 1,
            BookingReference = "RR-DETAIL01",
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
            Passengers = 3,
            Bags = 2,
            RequestedVehicleType = "Car",
            Notes = "Flight MH123, child seat please",
            PaymentMethod = "Bank_Transfer",
            EstimatedFare = 150m
        };

        private static DriverAssignmentListItemViewModel DriverTrip() => new()
        {
            BookingReference = "RR-DETAIL01",
            CustomerName = "Uncle Sim",
            CustomerPhone = "0125183838",
            FromName = "KLIA T1",
            ToName = "Mid Valley",
            PickupLocation = "Level 3, KLIA Terminal 1, Sepang",
            Destination = "Mid Valley Megamall, Kuala Lumpur",
            PickupDate = new DateOnly(2026, 10, 20),
            PickupTime = new TimeOnly(12, 0),
            DropOffDate = new DateOnly(2026, 10, 20),
            DropOffTime = new TimeOnly(15, 0),
            Passengers = 3,
            Bags = 2,
            RequestedVehicleType = "Car",
            Notes = "Flight MH123, child seat please"
        };

        private static string Value(TripDetailsViewModel details, string label) =>
            details.Rows().Single(r => r.Label == label).Value;

        [Fact]
        public void ForAdmin_ShowsEveryDetailIncludingEmailRemarksAndPayment()
        {
            var details = TripDetailsViewModel.ForAdmin(AdminBooking());

            Assert.Equal("Uncle Sim · 0125183838 · sim@email.com", Value(details, "Customer"));
            Assert.Equal("KLIA T1 → Mid Valley", Value(details, "Route"));
            Assert.Equal("2026-10-20 12:00 · Level 3, KLIA Terminal 1, Sepang", Value(details, "Pick up"));
            Assert.Equal("2026-10-20 15:00 · Mid Valley Megamall, Kuala Lumpur", Value(details, "Drop-off (approx.)"));
            Assert.Equal("3 pax · 2 bags · Car", Value(details, "Passengers & vehicle"));
            Assert.Equal("Flight MH123, child seat please", Value(details, "Remarks"));
            Assert.Equal("Bank Transfer", Value(details, "Payment"));
        }

        [Fact]
        public void ForDriver_ShowsTheTripButNeverEmailPaymentOrPrice()
        {
            var details = TripDetailsViewModel.ForDriver(DriverTrip());
            var everything = string.Join(" | ", details.Rows().Select(r => $"{r.Label}: {r.Value}"));

            Assert.Equal("Uncle Sim · 0125183838", Value(details, "Customer"));
            Assert.Equal("KLIA T1 → Mid Valley", Value(details, "Route"));
            Assert.Equal("2026-10-20 12:00 · Level 3, KLIA Terminal 1, Sepang", Value(details, "Pick up"));
            Assert.Equal("2026-10-20 15:00 · Mid Valley Megamall, Kuala Lumpur", Value(details, "Drop-off (approx.)"));
            Assert.Equal("3 pax · 2 bags · Car", Value(details, "Passengers & vehicle"));
            Assert.Equal("Flight MH123, child seat please", Value(details, "Remarks"));

            Assert.DoesNotContain(details.Rows(), r => r.Label == "Payment");
            Assert.DoesNotContain("@", everything);
            Assert.DoesNotContain("RM", everything);
            Assert.DoesNotContain("Transfer", everything);
            Assert.DoesNotContain("Pay at Pickup", everything);
        }

        [Fact]
        public void DriverViewModels_HaveNoFareEmailOrPaymentProperties()
        {
            // A guard so a price/payment/email field can't be added to the driver's data by accident.
            var forbidden = new[] { "Fare", "Price", "Payment", "Email", "Quote", "Amount", "Cost" };
            var driverTypes = new[] { typeof(DriverAssignmentListItemViewModel), typeof(TripDetailsViewModel) };

            foreach (var type in driverTypes)
            {
                var offenders = type.GetProperties()
                    .Select(p => p.Name)
                    // TripDetailsViewModel is shared with the admin card: it holds the admin-only values,
                    // but ForDriver must leave them null (asserted in the test above).
                    .Where(name => type == typeof(DriverAssignmentListItemViewModel) && forbidden.Any(f => name.Contains(f)))
                    .ToList();

                Assert.Empty(offenders);
            }
        }

        [Fact]
        public void Rows_UseBlankFallbacks()
        {
            var booking = AdminBooking();
            booking.Notes = null;
            booking.PaymentMethod = null;
            booking.CustomerEmail = null;
            booking.DropOffDate = null;
            booking.DropOffTime = null;
            booking.PickupLocation = string.Empty;
            booking.FromName = null;

            var details = TripDetailsViewModel.ForAdmin(booking);

            Assert.Equal("-", Value(details, "Remarks"));
            Assert.DoesNotContain(details.Rows(), r => r.Label == "Payment");
            Assert.Equal("Uncle Sim · 0125183838", Value(details, "Customer"));
            Assert.Equal("2026-10-20 12:00", Value(details, "Pick up"));
            Assert.Equal("Mid Valley Megamall, Kuala Lumpur", Value(details, "Drop-off (approx.)"));
            Assert.Equal("- → Mid Valley", Value(details, "Route"));
        }
    }
}
