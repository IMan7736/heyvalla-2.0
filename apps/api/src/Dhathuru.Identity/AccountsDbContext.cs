using Dhathuru.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Identity;

/// <summary>ASP.NET Core Identity store in its own schema with its own migration history.</summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public const string Schema = "identity";
    public const string MigrationsTable = "__ef_migrations";

    public DbSet<RefreshSession> Sessions => Set<RefreshSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();

    public DbSet<Traveller> Travellers => Set<Traveller>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "role_passenger", Name = Dhathuru.Domain.Security.Roles.Passenger, NormalizedName = "PASSENGER", ConcurrencyStamp = "role_passenger_v1" },
            new IdentityRole { Id = "role_operator", Name = Dhathuru.Domain.Security.Roles.Operator, NormalizedName = "OPERATOR", ConcurrencyStamp = "role_operator_v1" },
            new IdentityRole { Id = "role_admin", Name = Dhathuru.Domain.Security.Roles.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "role_admin_v1" });

        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(80);
            e.Property(u => u.Locale).HasMaxLength(16).IsRequired();
            e.Property(u => u.RiskTolerance).HasMaxLength(16).IsRequired();
            e.Property(u => u.PreferredModes).HasColumnType("text[]");
        });

        builder.Entity<RefreshSession>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(40);
            e.Property(s => s.UserId).HasMaxLength(450).IsRequired();
            e.Property(s => s.UserAgent).HasMaxLength(200);
            e.Property(s => s.NetworkHint).HasMaxLength(64);
            e.Property(s => s.RevokedReason).HasMaxLength(40);
            e.HasIndex(s => s.UserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.SessionId).HasMaxLength(40).IsRequired();
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.SessionId);
            e.HasOne(t => t.Session).WithMany(s => s.Tokens).HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountToken>(e =>
        {
            e.ToTable("account_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.UserId).HasMaxLength(450).IsRequired();
            e.Property(t => t.Purpose).HasMaxLength(24).IsRequired();
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(t => t.Payload).HasMaxLength(320);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UserId, t.Purpose });
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Traveller>(e =>
        {
            e.ToTable("travellers");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasMaxLength(40);
            e.Property(t => t.UserId).HasMaxLength(450).IsRequired();
            e.Property(t => t.FullName).HasMaxLength(120).IsRequired();
            e.Property(t => t.PassengerType).HasMaxLength(16).IsRequired();
            e.Property(t => t.Phone).HasMaxLength(32);
            e.Property(t => t.DocumentType).HasMaxLength(16).IsRequired();
            e.HasIndex(t => t.UserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
