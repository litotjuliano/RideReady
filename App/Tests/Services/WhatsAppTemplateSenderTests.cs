using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RideReady.Services;
using Xunit;

namespace RideReady.Tests.Services
{
    public class WhatsAppTemplateSenderTests
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string? Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }
        }

        private static async Task<JsonElement> SendAndCaptureAsync(WhatsAppSettings settings, string phone, string message)
        {
            var handler = new CapturingHandler();
            settings.ApiUrl = "https://graph.facebook.com/v18.0";
            settings.AccessToken = "test-token";
            settings.PhoneNumberId = "1234567890";
            var sender = new WhatsAppCloudApiSender(new HttpClient(handler), Options.Create(settings));

            await sender.SendAsync(phone, message);

            return JsonDocument.Parse(handler.Body!).RootElement;
        }

        [Fact]
        public async Task SendAsync_WithoutTemplateName_SendsPlainText()
        {
            var json = await SendAndCaptureAsync(new WhatsAppSettings(), "0125183838", "Hello there");

            Assert.Equal("text", json.GetProperty("type").GetString());
            Assert.Equal("Hello there", json.GetProperty("text").GetProperty("body").GetString());
            Assert.Equal("60125183838", json.GetProperty("to").GetString());
        }

        [Fact]
        public async Task SendAsync_WithTemplateName_SendsTemplateWithMessageAsBodyParameter()
        {
            var settings = new WhatsAppSettings { TemplateName = "rideready_update", TemplateLanguage = "en" };

            var json = await SendAndCaptureAsync(settings, "0125183838", "Your booking RR-123 is confirmed.");

            Assert.Equal("template", json.GetProperty("type").GetString());
            var template = json.GetProperty("template");
            Assert.Equal("rideready_update", template.GetProperty("name").GetString());
            Assert.Equal("en", template.GetProperty("language").GetProperty("code").GetString());
            var parameter = template.GetProperty("components")[0].GetProperty("parameters")[0];
            Assert.Equal("text", parameter.GetProperty("type").GetString());
            Assert.Equal("Your booking RR-123 is confirmed.", parameter.GetProperty("text").GetString());
        }

        [Theory]
        [InlineData("line one\nline two", "line one line two")]
        [InlineData("a\t\tb     c", "a b c")]
        [InlineData("  padded  ", "padded")]
        public void ToTemplateParameter_CollapsesWhitespaceThatMetaRejects(string input, string expected)
        {
            Assert.Equal(expected, WhatsAppCloudApiSender.ToTemplateParameter(input));
        }

        [Fact]
        public void ToTemplateParameter_TruncatesTo1024Characters()
        {
            Assert.Equal(1024, WhatsAppCloudApiSender.ToTemplateParameter(new string('x', 2000)).Length);
        }
    }
}
