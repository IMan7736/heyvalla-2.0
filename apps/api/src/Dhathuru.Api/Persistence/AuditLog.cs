using Dhathuru.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Api.Persistence;

/// <summary>Database backed <see cref="IAuditLog"/>. Adds request context (correlation id, coarse network, user agent) itself.</summary>
public sealed class AuditLog(
    IDbContextFactory<PlatformDbContext> factory,
    IHttpContextAccessor accessor,
    TimeProvider time,
    ILogger<AuditLog> logger) : IAuditLog
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        var http = accessor.HttpContext;
        var agent = http?.Request.Headers.UserAgent.ToString();
        var entry = new AuditEntry
        {
            At = time.GetUtcNow(),
            Action = auditEvent.Action,
            ActorUserId = auditEvent.ActorUserId,
            SubjectType = auditEvent.SubjectType,
            SubjectId = auditEvent.SubjectId,
            Outcome = auditEvent.Outcome.ToString().ToLowerInvariant(),
            CorrelationId = http?.Items[Middleware.CorrelationIdMiddleware.ItemKey] as string,
            NetworkHint = Middleware.ClientAddress.Truncated(http),
            UserAgent = string.IsNullOrWhiteSpace(agent) ? null : agent[..Math.Min(agent.Length, 200)],
            DataJson = auditEvent.Data is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(auditEvent.Data) : null,
        };

        try
        {
            await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
            db.AuditLog.Add(entry);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Never fail the user's request because the trail could not be written, but make the gap loud.
            logger.LogError(ex, "Audit entry {Action} could not be stored", auditEvent.Action);
        }
    }
}
