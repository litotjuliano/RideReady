using RideReady.ViewModels;
using Xunit;

namespace RideReady.Tests.ViewModels
{
    public class MustBeTrueAttributeTests
    {
        private readonly MustBeTrueAttribute _attribute = new();

        [Fact]
        public void IsValid_WhenTrue_ReturnsTrue() => Assert.True(_attribute.IsValid(true));

        [Fact]
        public void IsValid_WhenFalse_ReturnsFalse() => Assert.False(_attribute.IsValid(false));

        [Fact]
        public void IsValid_WhenNull_ReturnsFalse() => Assert.False(_attribute.IsValid(null));
    }
}
