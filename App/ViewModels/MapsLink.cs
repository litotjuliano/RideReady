namespace RideReady.ViewModels
{
    /// <summary>Opens an address in Google Maps (the Maps app on a phone).</summary>
    public static class MapsLink
    {
        public static string For(string? address) =>
            string.IsNullOrWhiteSpace(address)
                ? string.Empty
                : "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(address.Trim());
    }
}
