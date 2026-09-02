namespace RideReady.Models
{
    public class Driver
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty; // Car, Van, Bus
        public string? VehicleNumber { get; set; }
        public string PinHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public decimal? Rating { get; set; }
        public decimal CancellationRate { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DriverAssignment> Assignments { get; set; } = new List<DriverAssignment>();
        public ICollection<DriverLocation> Locations { get; set; } = new List<DriverLocation>();
    }
}
