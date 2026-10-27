using Dhathuru.Bookings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Bookings;

public sealed class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : DbContext(options)
{
    public const string Schema = "bookings";
    public const string MigrationsTable = "__ef_migrations";

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingLeg> BookingLegs => Set<BookingLeg>();

    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    public DbSet<RefundRecord> Refunds => Set<RefundRecord>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Disruption> Disruptions => Set<Disruption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);
    }
}
