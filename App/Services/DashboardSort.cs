namespace RideReady.Services
{
    /// <summary>Sort keys accepted by the admin dashboard's <c>?sort=</c> query parameter.</summary>
    public static class DashboardSort
    {
        public const string Pickup = "pickup";
        public const string Booked = "booked";

        /// <summary>Anything other than an explicit "pickup" falls back to the default: newest booking first.</summary>
        public static string Normalize(string? sort) => sort == Pickup ? Pickup : Booked;
    }
}
