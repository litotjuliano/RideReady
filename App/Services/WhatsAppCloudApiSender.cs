using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace RideReady.Services
{
    public class WhatsAppCloudApiSender : IWhatsAppSender
    {
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;

        public WhatsAppCloudApiSender(HttpClient httpClient, IOptions<WhatsAppSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task SendAsync(string toPhone, string message)
        {
            var url = $"{_settings.ApiUrl}/{_settings.PhoneNumberId}/messages";
            object payload = string.IsNullOrWhiteSpace(_settings.TemplateName)
                ? new
                {
                    messaging_product = "whatsapp",
                    to = NormalizePhone(toPhone),
                    type = "text",
                    text = new { body = message }
                }
                : new
                {
                    messaging_product = "whatsapp",
                    to = NormalizePhone(toPhone),
                    type = "template",
                    template = new
                    {
                        name = _settings.TemplateName,
                        language = new { code = string.IsNullOrWhiteSpace(_settings.TemplateLanguage) ? "en" : _settings.TemplateLanguage },
                        components = new[]
                        {
                            new
                            {
                                type = "body",
                                parameters = new[] { new { type = "text", text = ToTemplateParameter(message) } }
                            }
                        }
                    }
                };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"WhatsApp API request failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
            }
        }

        /// <summary>
        /// WhatsApp template parameters can't contain newlines, tabs or 4+ consecutive spaces, and are
        /// limited to 1024 characters.
        /// </summary>
        internal static string ToTemplateParameter(string message)
        {
            var singleLine = System.Text.RegularExpressions.Regex.Replace(message, @"\s+", " ").Trim();
            return singleLine.Length <= 1024 ? singleLine : singleLine[..1024];
        }

        internal static string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.StartsWith('0') ? "60" + digits[1..] : digits;
        }
    }
}
