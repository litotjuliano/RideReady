namespace RideReady.Services
{
    /// <summary>Sort keys accepted by the admin dashboard's <c>?sort=</c> query parameter.</summary>
    public static class DashboardSort
    {
        public const string Pickup = "pickup";
        public const string Booked = "booked";

        public static string Normalize(string? sort) => sort == Booked ? Booked : Pickup;
    }
}
