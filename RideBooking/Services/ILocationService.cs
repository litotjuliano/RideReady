namespace RideReady.Services
{
    public interface ILocationService
    {
        Task<decimal> GetDistanceAsync(string pickup, string destination);
        Task<decimal> GetDurationAsync(string pickup, string destination);
    }

    public class MockLocationService : ILocationService
    {
        public Task<decimal> GetDistanceAsync(string pickup, string destination)
        {
            return Task.FromResult(215m); // Mock: KL to Ipoh is ~215km
        }

        public Task<decimal> GetDurationAsync(string pickup, string destination)
        {
            return Task.FromResult(2.5m); // Mock: 2.5 hours
        }
    }
}
