using Microsoft.EntityFrameworkCore;
using RideBooking.Models;

namespace RideBooking.Data
{
    public class RideBookingDbContext : DbContext
    {
        public RideBookingDbContext(DbContextOptions<RideBookingDbContext> options)
            : base(options) { }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<DriverAssignment> DriverAssignments => Set<DriverAssignment>();
        public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
        public DbSet<BookingQuote> BookingQuotes => Set<BookingQuote>();
        public DbSet<PricingSetting> PricingSettings => Set<PricingSetting>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
        public DbSet<OperatorCalendarEvent> OperatorCalendarEvents => Set<OperatorCalendarEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes
            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.Status, b.PickupDate });

            modelBuilder.Entity<DriverLocation>()
                .HasIndex(dl => new { dl.DriverId, dl.BookingId, dl.RecordedAt })
                .IsDescending(false, false, true);

            modelBuilder.Entity<DriverAssignment>()
                .HasIndex(da => new { da.BookingId, da.DriverId })
                .IsUnique();

            // Relationships
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingQuote>()
                .HasOne(bq => bq.Booking)
                .WithOne(b => b.Quote)
                .HasForeignKey<BookingQuote>(bq => bq.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
