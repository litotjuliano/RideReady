using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class TripTextTests
    {
        [Fact]
        public void Place_PrefersTheShortName()
        {
            Assert.Equal("KLIA T1", TripText.Place("  KLIA T1 ", "Level 3, KLIA Terminal 1, Sepang"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Place_FallsBackToTheAddress_WhenNoShortName(string? shortName)
        {
            Assert.Equal("Mid Valley Megamall", TripText.Place(shortName, " Mid Valley Megamall "));
        }

        [Fact]
        public void Place_ReturnsADash_WhenNeitherIsGiven()
        {
            Assert.Equal("-", TripText.Place(null, ""));
        }

        [Fact]
        public void Route_JoinsFromAndTo_UsingNamesWhenGivenElseAddresses()
        {
            Assert.Equal("KLIA T1 -> Mid Valley", TripText.Route("KLIA T1", "Level 3 KLIA", "Mid Valley", "Mid Valley Megamall"));
            Assert.Equal("Level 3 KLIA -> Mid Valley Megamall", TripText.Route(null, "Level 3 KLIA", null, "Mid Valley Megamall"));
            Assert.Equal("- -> Mid Valley Megamall", TripText.Route(null, null, null, "Mid Valley Megamall"));
        }

        [Fact]
        public void DropOffAt_FormatsDateAndTime_OrReturnsEmpty()
        {
            Assert.Equal("2026-10-20 15:00", TripText.DropOffAt(new DateOnly(2026, 10, 20), new TimeOnly(15, 0)));
            Assert.Equal(string.Empty, TripText.DropOffAt(null, new TimeOnly(15, 0)));
            Assert.Equal(string.Empty, TripText.DropOffAt(new DateOnly(2026, 10, 20), null));
        }
    }
}
