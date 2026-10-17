using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class DashboardAttentionTests
    {
        // 02:00 UTC on 20 Oct 2026 is 10:00 in Malaysia (UTC+8).
        private static readonly DateTime UtcNow = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly Today = new(2026, 10, 20);

        private static string? Alert(string status, TimeOnly pickupAt, DateOnly? date = null) =>
            DashboardAttention.PickupAlert(status, date ?? Today, pickupAt, UtcNow);

        [Theory]
        [InlineData(10, 0)]   // right now
        [InlineData(10, 30)]
        [InlineData(11, 59)]
        [InlineData(12, 0)]   // exactly 2 hours away still counts
        public void PickupWithinTwoHours_IsFlaggedSoon(int hour, int minute)
        {
            Assert.Equal(DashboardAttention.PickupSoon, Alert("Confirmed", new TimeOnly(hour, minute)));
        }

        [Theory]
        [InlineData(12, 1)]
        [InlineData(15, 0)]
        [InlineData(23, 45)]
        public void PickupMoreThanTwoHoursAway_IsNotFlagged(int hour, int minute)
        {
            Assert.Null(Alert("Confirmed", new TimeOnly(hour, minute)));
        }

        [Fact]
        public void PickupOnALaterDate_IsNotFlagged()
        {
            Assert.Null(Alert("Confirmed", new TimeOnly(10, 30), new DateOnly(2026, 10, 21)));
        }

        [Theory]
        [InlineData(9, 59)]
        [InlineData(6, 0)]
        public void PickupTimePassedAndNotPickedUp_IsFlaggedOverdue(int hour, int minute)
        {
            Assert.Equal(DashboardAttention.PickupOverdue, Alert("Confirmed", new TimeOnly(hour, minute)));
        }

        [Fact]
        public void PickupYesterday_IsFlaggedOverdue()
        {
            Assert.Equal(DashboardAttention.PickupOverdue, Alert("New", new TimeOnly(12, 0), new DateOnly(2026, 10, 19)));
        }

        [Theory]
        [InlineData("New")]
        [InlineData("Driver_Assigned")]
        [InlineData("Confirmed")]
        public void BookingsNotYetPickedUp_CanBeFlagged(string status)
        {
            Assert.Equal(DashboardAttention.PickupSoon, Alert(status, new TimeOnly(11, 0)));
        }

        [Theory]
        [InlineData("Picked_Up")]
        [InlineData("In_Transit")]
        [InlineData("Dropped_Off")]
        [InlineData("Completed")]
        [InlineData("Cancelled")]
        [InlineData("No_Show")]
        public void StartedOrClosedBookings_AreNeverFlagged(string status)
        {
            Assert.Null(Alert(status, new TimeOnly(11, 0)));   // would be "soon"
            Assert.Null(Alert(status, new TimeOnly(6, 0)));    // would be "overdue"
        }

        [Fact]
        public void UsesMalaysiaTime_NotUtc()
        {
            // 10:30 is 30 minutes away in Malaysia. If the clock were read as UTC it would be 8.5 hours away.
            Assert.Equal(DashboardAttention.PickupSoon, Alert("Confirmed", new TimeOnly(10, 30)));
        }

        [Fact]
        public void WorksAcrossMidnightInMalaysia()
        {
            // 17:00 UTC on the 20th is 01:00 on the 21st in Malaysia.
            var lateUtc = new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc);

            var alert = DashboardAttention.PickupAlert("Confirmed", new DateOnly(2026, 10, 21), new TimeOnly(2, 0), lateUtc);

            Assert.Equal(DashboardAttention.PickupSoon, alert);
        }
    }
}
