namespace Dhathuru.Domain.Security;

/// <summary>Append-only record of a security-relevant event. <c>Data</c> must never hold secrets, tokens, passwords or raw document numbers.</summary>
public sealed record AuditEvent(
    string Action,
    string? ActorUserId,
    string? SubjectType = null,
    string? SubjectId = null,
    AuditOutcome Outcome = AuditOutcome.Success,
    IReadOnlyDictionary<string, string>? Data = null);

public enum AuditOutcome
{
    Success,
    Failure,
    Denied,
}

/// <summary>
/// Audit trail seam: who did what, when. Request context (correlation id, truncated client address, user agent)
/// is added by the implementation, so modules only describe the event.
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken ct);
}
