using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RideReady.Controllers;
using Xunit;

namespace RideReady.Tests.Controllers
{
    public class DriverAuthControllerTests
    {
        [Fact]
        public void Logout_RequiresDriverAuthScheme()
        {
            // Regression guard - same root cause as AdminAuthControllerTests.Logout_RequiresAdminAuthScheme,
            // for the driver-side cookie scheme.
            var method = typeof(DriverAuthController).GetMethod(nameof(DriverAuthController.Logout));

            var authorizeAttribute = method!.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("DriverAuth", authorizeAttribute!.AuthenticationSchemes);
        }
    }
}
