using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class BookingProgressTests
    {
        private static string States(string? status) =>
            string.Join(",", BookingProgress.GetSteps(status).Select(s => s.State.ToString()[0]));

        [Fact]
        public void GetSteps_ReturnsTheSevenLifecycleStepsInOrder()
        {
            var steps = BookingProgress.GetSteps("New");

            Assert.Equal(7, steps.Count);
            Assert.Equal(7, BookingProgress.TotalSteps);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, steps.Select(s => s.Number));
            Assert.Equal(
                new[] { "New", "Assigned", "Confirmed", "Picked up", "In transit", "Dropped off", "Completed" },
                steps.Select(s => s.Label));
        }

        [Theory]
        [InlineData("New", "C,U,U,U,U,U,U")]
        [InlineData("Driver_Assigned", "D,C,U,U,U,U,U")]
        [InlineData("Confirmed", "D,D,C,U,U,U,U")]
        [InlineData("Picked_Up", "D,D,D,C,U,U,U")]
        [InlineData("In_Transit", "D,D,D,D,C,U,U")]
        [InlineData("Dropped_Off", "D,D,D,D,D,C,U")]
        [InlineData("Completed", "D,D,D,D,D,D,D")]
        public void GetSteps_MarksStepsBeforeCurrentDone_CurrentCurrent_RestUpcoming(string status, string expected)
        {
            Assert.Equal(expected, States(status));
        }

        [Theory]
        [InlineData("Cancelled", "Cancelled")]
        [InlineData("No_Show", "No-show")]
        public void OffTrackStatuses_ShowALabelAndLightNoSteps(string status, string label)
        {
            Assert.Equal(label, BookingProgress.OffTrackLabel(status));
            Assert.Equal("U,U,U,U,U,U,U", States(status));
        }

        [Theory]
        [InlineData("New")]
        [InlineData("Completed")]
        [InlineData("something_else")]
        [InlineData(null)]
        public void OnTrackAndUnknownStatuses_HaveNoOffTrackLabel(string? status)
        {
            Assert.Null(BookingProgress.OffTrackLabel(status));
        }

        [Fact]
        public void UnknownOrNullStatus_LightsNothingAndDoesNotThrow()
        {
            Assert.Equal("U,U,U,U,U,U,U", States("something_else"));
            Assert.Equal("U,U,U,U,U,U,U", States(null));
        }
    }
}
