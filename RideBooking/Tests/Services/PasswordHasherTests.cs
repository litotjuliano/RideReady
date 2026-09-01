using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ThenVerify_WithCorrectPlainText_ReturnsTrue()
        {
            var hash = PasswordHasher.Hash("1234");
            Assert.True(PasswordHasher.Verify("1234", hash));
        }

        [Fact]
        public void Hash_ThenVerify_WithWrongPlainText_ReturnsFalse()
        {
            var hash = PasswordHasher.Hash("1234");
            Assert.False(PasswordHasher.Verify("9999", hash));
        }
    }
}
