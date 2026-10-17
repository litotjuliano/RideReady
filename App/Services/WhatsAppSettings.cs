namespace RideReady.Services
{
    public class WhatsAppSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string OperatorPhone { get; set; } = string.Empty;

        /// <summary>
        /// Name of an approved WhatsApp message template with a single body variable ({{1}}).
        /// When set, every message is sent as that template with the message text as the variable, so it
        /// can reach recipients who haven't messaged the business in the last 24 hours. When empty, plain
        /// text is sent (only delivered inside the 24-hour customer-service window).
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>Language code the template was approved in (e.g. "en", "en_US").</summary>
        public string TemplateLanguage { get; set; } = "en";
    }
}
