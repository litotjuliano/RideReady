using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RideBooking.Services;
using Xunit;

namespace RideBooking.Tests.Services
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson)
            };
            return Task.FromResult(response);
        }
    }

    public class GoogleMapsLocationServiceTests
    {
        private const string SampleDirectionsResponse = @"{
            ""status"": ""OK"",
            ""routes"": [{
                ""legs"": [{
                    ""distance"": { ""value"": 215000, ""text"": ""215 km"" },
                    ""duration"": { ""value"": 9000, ""text"": ""2 hours 30 mins"" }
                }]
            }]
        }";

        [Fact]
        public async Task GetDistanceAsync_WithValidResponse_ReturnsDistanceInKm()
        {
            // Arrange
            var (service, _) = CreateService(SampleDirectionsResponse);

            // Act
            var distance = await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(215m, distance);
        }

        [Fact]
        public async Task GetDurationAsync_WithValidResponse_ReturnsDurationInHours()
        {
            // Arrange
            var (service, _) = CreateService(SampleDirectionsResponse);

            // Act
            var duration = await service.GetDurationAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(2.5m, duration);
        }

        [Fact]
        public async Task GetDistanceThenDuration_ForSameRoute_OnlyCallsApiOnce()
        {
            // Arrange
            var (service, handler) = CreateService(SampleDirectionsResponse);

            // Act
            await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");
            await service.GetDurationAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task GetDistanceAsync_WithNonOkStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var errorResponse = @"{ ""status"": ""ZERO_RESULTS"", ""routes"": [] }";
            var (service, _) = CreateService(errorResponse);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetDistanceAsync("Nowhere", "Nowhere Else"));
        }

        [Fact]
        public async Task GetDistanceAsync_WithOkStatusButNoRoutes_ThrowsInvalidOperationException()
        {
            // Arrange: Google can return status "OK" with an empty routes array
            // (e.g. some edge-case waypoint combinations); this must not throw
            // an IndexOutOfRangeException.
            var malformedResponse = @"{ ""status"": ""OK"", ""routes"": [] }";
            var (service, _) = CreateService(malformedResponse);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetDistanceAsync("Nowhere", "Nowhere Else"));
        }

        [Fact]
        public async Task GetDistanceAsync_WithNonSuccessHttpStatus_ThrowsHttpRequestException()
        {
            // Arrange
            var (service, _) = CreateService("{}", HttpStatusCode.InternalServerError);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.GetDistanceAsync("Nowhere", "Nowhere Else"));
        }

        private static (GoogleMapsLocationService Service, FakeHttpMessageHandler Handler) CreateService(
            string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handler = new FakeHttpMessageHandler(responseJson, statusCode);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);
            return (service, handler);
        }
    }
}
