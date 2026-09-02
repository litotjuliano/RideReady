namespace RideReady.Services
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string OperatorEmail { get; set; } = string.Empty;
    }
}
