using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
{
    /// <summary>
    /// Verifies that AddHttpClient's default logging handlers do not leak the
    /// billable Google Maps API key (carried in the request URL's query
    /// string) into application logs at the app's configured default log
    /// level. This exercises the real IHttpClientFactory pipeline plus the
    /// project's actual appsettings.json "Logging" section, so it fails if
    /// the log-level override for the GoogleMapsLocationService HttpClient
    /// categories is ever removed or the app's default level is lowered.
    /// </summary>
    public class GoogleMapsLocationServiceLoggingTests
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
        public async Task GetDistanceAsync_ThroughRealHttpClientFactoryPipeline_NeverLogsApiKey()
        {
            // Arrange: load the app's real appsettings.json "Logging" section so
            // this test would fail if the log-level override protecting the API
            // key were ever removed from configuration.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            const string secretApiKey = "super-secret-directions-api-key";
            var capturedMessages = new ConcurrentBag<string>();

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddProvider(new CapturingLoggerProvider(capturedMessages));
            });
            services.AddMemoryCache();
            services.Configure<GoogleMapsSettings>(o => o.ApiKey = secretApiKey);
            services.AddHttpClient<GoogleMapsLocationService>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler(SampleDirectionsResponse));

            using var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<GoogleMapsLocationService>();

            // Act
            await service.GetDistanceAsync("KL Sentral", "KLIA Terminal 1");

            // Assert: the app's default logging configuration must never emit
            // the API key, whether via the request URL or otherwise. (Verified
            // this test is not vacuous: without the appsettings.json log-level
            // override for the GoogleMapsLocationService HttpClient categories,
            // this fails with captured messages "Start processing HTTP request
            // GET https://maps.googleapis.com/...&key=<secret>..." and "Sending
            // HTTP request GET https://maps.googleapis.com/...&key=<secret>...".)
            Assert.DoesNotContain(capturedMessages, message => message.Contains(secretApiKey));
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentBag<string> _messages;

            public CapturingLoggerProvider(ConcurrentBag<string> messages)
            {
                _messages = messages;
            }

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

            public void Dispose()
            {
            }

            private sealed class CapturingLogger : ILogger
            {
                private readonly ConcurrentBag<string> _messages;

                public CapturingLogger(ConcurrentBag<string> messages)
                {
                    _messages = messages;
                }

                public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                    _messages.Add(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose()
                {
                }
            }
        }
    }
}
