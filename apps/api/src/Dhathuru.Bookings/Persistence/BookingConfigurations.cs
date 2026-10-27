using Dhathuru.Bookings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhathuru.Bookings.Persistence;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("bookings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.OptionId).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.ContactEmail).HasMaxLength(254);
        b.Property(x => x.Locale).HasMaxLength(16);
        b.Property(x => x.CreateKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.CreateRequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.PriceLinesJson).HasColumnType("jsonb");
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.UserId, x.CreateKey }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.HoldExpiresAt });
        b.HasMany(x => x.Legs).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Passengers).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Tickets).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Audit).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Disruptions).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Legs).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Passengers).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Tickets).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Audit).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Disruptions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.Total);
    }
}

internal sealed class BookingLegConfiguration : IEntityTypeConfiguration<BookingLeg>
{
    public void Configure(EntityTypeBuilder<BookingLeg> b)
    {
        b.ToTable("booking_legs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.LegJson).HasColumnType("jsonb");
        b.Property(x => x.ProviderId).HasMaxLength(128).IsRequired();
        b.Property(x => x.ServiceRunId).HasMaxLength(128);
        b.Property(x => x.HoldReference).HasMaxLength(128);
        b.Property(x => x.ProviderReference).HasMaxLength(128);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.HasIndex(x => new { x.BookingId, x.LegIndex }).IsUnique();
        b.HasIndex(x => new { x.ProviderId, x.ServiceRunId });
    }
}

internal sealed class BookingPassengerConfiguration : IEntityTypeConfiguration<BookingPassenger>
{
    public void Configure(EntityTypeBuilder<BookingPassenger> b)
    {
        b.ToTable("booking_passengers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.TravellerId).HasMaxLength(40);
        b.Property(x => x.Phone).HasMaxLength(32);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Gateway).HasMaxLength(16);
        b.Property(x => x.GatewayReference).HasMaxLength(128);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.ChallengeId).HasMaxLength(64);
        b.HasIndex(x => new { x.BookingId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.BookingId, x.Attempt }).IsUnique();
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<RefundRecord>
{
    public void Configure(EntityTypeBuilder<RefundRecord> b)
    {
        b.ToTable("refunds");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.PaymentId).HasMaxLength(40);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ReasonCode).HasMaxLength(64);
        b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.BookingLegId).HasMaxLength(40);
        b.Property(x => x.PassengerId).HasMaxLength(40);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Nonce).HasMaxLength(64);
        b.Property(x => x.KeyId).HasMaxLength(32);
        b.HasIndex(x => new { x.BookingLegId, x.PassengerId }).IsUnique();
    }
}

internal sealed class AuditConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_entries");
        b.HasKey(x => x.Id);
        // Ids are assigned by the domain; without this EF would treat a new entry with a Guid key as already stored.
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.Action).HasMaxLength(64);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Actor).HasMaxLength(64);
        b.Property(x => x.Detail).HasMaxLength(1024);
        b.HasIndex(x => new { x.BookingId, x.At });
    }
}

internal sealed class DisruptionConfiguration : IEntityTypeConfiguration<Disruption>
{
    public void Configure(EntityTypeBuilder<Disruption> b)
    {
        b.ToTable("disruptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(40);
        b.Property(x => x.BookingId).HasMaxLength(40);
        b.Property(x => x.BookingLegId).HasMaxLength(40);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SuggestionJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.BookingLegId, x.State });
    }
}
