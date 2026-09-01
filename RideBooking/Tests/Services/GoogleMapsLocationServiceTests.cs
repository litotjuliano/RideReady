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
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act
            var distance = await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(215m, distance);
        }

        [Fact]
        public async Task GetDurationAsync_WithValidResponse_ReturnsDurationInHours()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act
            var duration = await service.GetDurationAsync("KL Sentral", "KLIA Terminal 1");

            // Assert
            Assert.Equal(2.5m, duration);
        }

        [Fact]
        public async Task GetDistanceThenDuration_ForSameRoute_OnlyCallsApiOnce()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(SampleDirectionsResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

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
            var handler = new FakeHttpMessageHandler(errorResponse);
            var httpClient = new HttpClient(handler);
            var settings = Options.Create(new GoogleMapsSettings { ApiKey = "test-key" });
            var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new GoogleMapsLocationService(httpClient, settings, cache);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetDistanceAsync("Nowhere", "Nowhere Else"));
        }
    }
}
