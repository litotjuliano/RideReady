using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class NoPaymentMethodTests
    {
        [Fact]
        public void TheBookingFormHasNoPaymentMethod()
        {
            Assert.Null(typeof(BookingRequestViewModel).GetProperty("PaymentMethod"));
        }

        [Fact]
        public void TheDashboardAndTripDetailsCarryNoPaymentMethod()
        {
            Assert.Null(typeof(AdminBookingListItemViewModel).GetProperty("PaymentMethod"));
            Assert.Null(typeof(TripDetailsViewModel).GetProperty("PaymentMethod"));
        }
    }
}
