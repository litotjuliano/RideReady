using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RideBooking.Services
{
    public class GoogleMapsLocationService : ILocationService
    {
        private readonly HttpClient _httpClient;
        private readonly GoogleMapsSettings _settings;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        public GoogleMapsLocationService(HttpClient httpClient, IOptions<GoogleMapsSettings> settings, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _cache = cache;
        }

        public async Task<decimal> GetDistanceAsync(string pickup, string destination)
        {
            var route = await GetRouteAsync(pickup, destination);
            return route.DistanceKm;
        }

        public async Task<decimal> GetDurationAsync(string pickup, string destination)
        {
            var route = await GetRouteAsync(pickup, destination);
            return route.DurationHours;
        }

        private async Task<(decimal DistanceKm, decimal DurationHours)> GetRouteAsync(string pickup, string destination)
        {
            var cacheKey = $"route:{pickup.Trim().ToLowerInvariant()}|{destination.Trim().ToLowerInvariant()}";

            if (_cache.TryGetValue(cacheKey, out (decimal DistanceKm, decimal DurationHours) cached))
            {
                return cached;
            }

            var url = "https://maps.googleapis.com/maps/api/directions/json" +
                $"?origin={Uri.EscapeDataString(pickup)}" +
                $"&destination={Uri.EscapeDataString(destination)}" +
                $"&key={_settings.ApiKey}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var route = ParseRoute(json);

            _cache.Set(cacheKey, route, CacheDuration);
            return route;
        }

        internal static (decimal DistanceKm, decimal DurationHours) ParseRoute(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString();

            if (status != "OK")
            {
                throw new InvalidOperationException($"Google Directions API returned status: {status}");
            }

            var routes = root.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Google Directions API returned no route");
            }

            var legs = routes[0].GetProperty("legs");
            if (legs.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Google Directions API returned no route");
            }

            var leg = legs[0];
            var distanceMeters = leg.GetProperty("distance").GetProperty("value").GetInt64();
            var durationSeconds = leg.GetProperty("duration").GetProperty("value").GetInt64();

            return (distanceMeters / 1000m, durationSeconds / 3600m);
        }
    }
}
