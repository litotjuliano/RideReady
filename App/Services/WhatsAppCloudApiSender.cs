using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RideReady.ViewModels;

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

            await PostAsync(payload);
        }

        public async Task SendTemplateAsync(string toPhone, string templateName, IReadOnlyList<string> parameters)
        {
            // Meta rejects an empty variable, so a blank detail is sent as a dash.
            var variables = parameters
                .Select(p => ToTemplateParameter(p) is { Length: > 0 } text ? text : "-")
                .Select(text => new { type = "text", text })
                .ToArray();

            await PostAsync(new
            {
                messaging_product = "whatsapp",
                to = NormalizePhone(toPhone),
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = string.IsNullOrWhiteSpace(_settings.TemplateLanguage) ? "en" : _settings.TemplateLanguage },
                    components = new[] { new { type = "body", parameters = variables } }
                }
            });
        }

        private async Task PostAsync(object payload)
        {
            var url = $"{_settings.ApiUrl}/{_settings.PhoneNumberId}/messages";
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
            return PhoneLinks.ToInternational(phone);
        }
    }
}
