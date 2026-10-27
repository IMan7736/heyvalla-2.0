using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Api.Persistence;

public sealed class AuditEntry
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    public string Action { get; set; } = "";

    public string? ActorUserId { get; set; }

    public string? SubjectType { get; set; }

    public string? SubjectId { get; set; }

    public string Outcome { get; set; } = "success";

    public string? CorrelationId { get; set; }

    public string? NetworkHint { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Small JSON object of string values. Never secrets.</summary>
    public string? DataJson { get; set; }
}

public static class IdempotencyState
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
}

public sealed class IdempotencyRecord
{
    /// <summary>The user id, or "anonymous". Keys are namespaced per caller so one user can never replay another's response.</summary>
    public string Scope { get; set; } = "";

    public string Key { get; set; } = "";

    public string RequestHash { get; set; } = "";

    public string State { get; set; } = IdempotencyState.InProgress;

    public int? StatusCode { get; set; }

    public string? ContentType { get; set; }

    public byte[]? ResponseBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>Host-level storage (audit trail and idempotency keys) in the <c>platform</c> schema with its own migration history.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";
    public const string MigrationsTable = "__ef_migrations";

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(64).IsRequired();
            e.Property(a => a.ActorUserId).HasMaxLength(64);
            e.Property(a => a.SubjectType).HasMaxLength(32);
            e.Property(a => a.SubjectId).HasMaxLength(64);
            e.Property(a => a.Outcome).HasMaxLength(16).IsRequired();
            e.Property(a => a.CorrelationId).HasMaxLength(64);
            e.Property(a => a.NetworkHint).HasMaxLength(64);
            e.Property(a => a.UserAgent).HasMaxLength(200);
            e.Property(a => a.DataJson).HasMaxLength(2000);
            e.HasIndex(a => new { a.ActorUserId, a.At });
            e.HasIndex(a => a.At);
        });

        builder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_keys");
            e.HasKey(r => new { r.Scope, r.Key });
            e.Property(r => r.Scope).HasMaxLength(64);
            e.Property(r => r.Key).HasMaxLength(128);
            e.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
            e.Property(r => r.State).HasMaxLength(16).IsRequired();
            e.Property(r => r.ContentType).HasMaxLength(128);
            e.HasIndex(r => r.ExpiresAt);
        });
    }
}
