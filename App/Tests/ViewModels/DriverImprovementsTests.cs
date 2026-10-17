using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class DriverNextStepTests
    {
        [Theory]
        [InlineData("Confirmed", "Picked_Up", "Picked up")]
        [InlineData("Picked_Up", "In_Transit", "Start trip")]
        [InlineData("In_Transit", "Dropped_Off", "Dropped off")]
        [InlineData("Dropped_Off", "Completed", "Complete trip")]
        public void EachStatusLeadsToTheNextOne(string current, string next, string label)
        {
            var step = DriverNextStep.For(current);

            Assert.NotNull(step);
            Assert.Equal(next, step!.NewStatus);
            Assert.Equal(label, step.ButtonLabel);
        }

        [Theory]
        [InlineData("New")]
        [InlineData("Driver_Assigned")]
        [InlineData("Completed")]
        [InlineData("Cancelled")]
        [InlineData("No_Show")]
        [InlineData("")]
        public void NoNextStepWhenTheDriverCannotMoveTheTripOn(string current)
        {
            Assert.Null(DriverNextStep.For(current));
        }
    }

    public class MapsLinkTests
    {
        [Fact]
        public void AddressIsEncodedIntoAMapsSearch()
        {
            Assert.Equal(
                "https://www.google.com/maps/search/?api=1&query=Level%203%2C%20KLIA%20Terminal%201%20%26%20more",
                MapsLink.For("Level 3, KLIA Terminal 1 & more"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoAddress_GivesNoLink(string? address)
        {
            Assert.Equal(string.Empty, MapsLink.For(address));
        }
    }
}
