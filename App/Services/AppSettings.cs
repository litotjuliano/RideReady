namespace RideReady.Services
{
    public class AppSettings
    {
        /// <summary>
        /// Public address of the site, for example https://rideready.litxus.com (no trailing slash needed).
        /// When empty, messages are sent without sign-in links.
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
