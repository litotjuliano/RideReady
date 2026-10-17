using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RideReady.Controllers;
using Xunit;

namespace RideReady.Tests.Controllers
{
    public class AdminAuthControllerTests
    {
        [Fact]
        public void Logout_RequiresAdminAuthScheme()
        {
            // Regression guard: Program.cs registers two independent cookie auth schemes
            // (AdminAuth, DriverAuth) with no default scheme. ASP.NET Core's antiforgery
            // system binds each token to HttpContext.User.Identity.Name at render time and
            // re-checks it on submit. Without this attribute, nothing authenticates the
            // AdminAuth scheme for this specific request, so HttpContext.User stays
            // anonymous here even though the page that rendered the sign-out form was
            // authenticated as "admin" - antiforgery then rejects every logout attempt
            // with a 400 ("meant for a different claims-based user than the current
            // user"), and sign-out silently never works.
            var method = typeof(AdminAuthController).GetMethod(nameof(AdminAuthController.Logout));

            var authorizeAttribute = method!.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("AdminAuth", authorizeAttribute!.AuthenticationSchemes);
        }
    }
}
