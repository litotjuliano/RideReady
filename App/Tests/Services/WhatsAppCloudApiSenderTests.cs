using System.Net;
using Microsoft.Extensions.Options;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
{
    public class WhatsAppCloudApiSenderTests
    {
        private static (WhatsAppCloudApiSender Sender, FakeHttpMessageHandler Handler) CreateSender(
            string responseBody, HttpStatusCode statusCode)
        {
            var handler = new FakeHttpMessageHandler(responseBody, statusCode);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new WhatsAppSettings
            {
                ApiUrl = "https://graph.facebook.com/v18.0",
                AccessToken = "test-token",
                PhoneNumberId = "1234567890"
            });
            var sender = new WhatsAppCloudApiSender(httpClient, settings);
            return (sender, handler);
        }

        [Fact]
        public async Task SendAsync_WithSuccessResponse_CompletesWithoutThrowing()
        {
            // Arrange
            var (sender, handler) = CreateSender("{\"messages\":[{\"id\":\"wamid.abc\"}]}", HttpStatusCode.OK);

            // Act
            await sender.SendAsync("0123456789", "Test message");

            // Assert
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_WithFailureResponse_ThrowsExceptionContainingMetaErrorBody()
        {
            // Arrange
            const string errorBody = "{\"error\":{\"message\":\"Invalid OAuth access token\",\"type\":\"OAuthException\",\"code\":190}}";
            var (sender, _) = CreateSender(errorBody, HttpStatusCode.Unauthorized);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync("0123456789", "Test message"));
            Assert.Contains("Invalid OAuth access token", ex.Message);
            Assert.Contains("401", ex.Message);
        }
    }
}
