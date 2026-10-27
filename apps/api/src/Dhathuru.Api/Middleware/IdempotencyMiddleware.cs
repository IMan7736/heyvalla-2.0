using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dhathuru.Api.Persistence;
using Dhathuru.Domain.Http;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Api.Middleware;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>How long a key is remembered.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>Largest request body that can be hashed and replayed.</summary>
    public int MaxRequestBytes { get; set; } = 256 * 1024;

    /// <summary>Largest response body that is stored for replay.</summary>
    public int MaxResponseBytes { get; set; } = 256 * 1024;
}

/// <summary>
/// Implements the <c>Idempotency-Key</c> contract for endpoints marked with <c>RequireIdempotency()</c>.
/// <list type="bullet">
/// <item>First request: runs the endpoint and stores the response (anything below 500) next to a hash of the request.</item>
/// <item>Same key and same request: replays the stored response with <c>Idempotent-Replayed: true</c>.</item>
/// <item>Same key, different request: 409 <c>IDEMPOTENCY_KEY_REUSED</c>.</item>
/// <item>Same key while the first is still running: 409 <c>IDEMPOTENCY_IN_PROGRESS</c> (retry shortly).</item>
/// </list>
/// Keys are scoped to the caller, so one user can never read another user's stored response.
/// Server errors are not stored, so a retry after a 5xx really retries.
/// </summary>
public sealed partial class IdempotencyMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<IdempotencyOptions> options)
{
    public async Task InvokeAsync(HttpContext context, IDbContextFactory<PlatformDbContext> factory, TimeProvider time)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyRequiredMetadata>() is null
            || HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var key = context.Request.Headers[IdempotencyExtensions.HeaderName].ToString();
        if (string.IsNullOrEmpty(key)) throw new ProblemException(StatusCodes.Status400BadRequest, "IDEMPOTENCY_KEY_REQUIRED");
        if (!ValidKey().IsMatch(key)) throw new ProblemException(StatusCodes.Status400BadRequest, "IDEMPOTENCY_KEY_INVALID");

        var settings = options.Value;
        var scope = context.User.FindFirst("sub")?.Value ?? "anonymous";
        var requestHash = await HashRequestAsync(context, settings.MaxRequestBytes);
        var now = time.GetUtcNow();

        await using var db = await factory.CreateDbContextAsync(context.RequestAborted);

        // Forget expired keys so they can be reused.
        await db.IdempotencyKeys.Where(r => r.Scope == scope && r.Key == key && r.ExpiresAt <= now).ExecuteDeleteAsync(context.RequestAborted);

        // ON CONFLICT DO NOTHING instead of catching a unique-violation: a legitimate retry is not an error, and a failed
        // INSERT is logged by the database driver as one (L-13).
        var expiresAt = now.AddHours(settings.RetentionHours);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO platform.idempotency_keys ("Scope", "Key", "RequestHash", "State", "CreatedAt", "ExpiresAt")
            VALUES ({scope}, {key}, {requestHash}, {IdempotencyState.InProgress}, {now}, {expiresAt})
            ON CONFLICT ("Scope", "Key") DO NOTHING
            """, context.RequestAborted);
        if (inserted == 0)
        {
            var existing = await db.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, context.RequestAborted);
            if (existing is null)
            {
                // Removed between the conflict and the read (a failed first attempt cleaned up): tell the client to retry.
                context.Response.Headers.RetryAfter = "1";
                throw ProblemException.Conflict("IDEMPOTENCY_IN_PROGRESS");
            }

            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw ProblemException.Conflict("IDEMPOTENCY_KEY_REUSED");
            }

            if (existing.State != IdempotencyState.Completed)
            {
                context.Response.Headers.RetryAfter = "1";
                throw ProblemException.Conflict("IDEMPOTENCY_IN_PROGRESS");
            }

            context.Response.StatusCode = existing.StatusCode ?? StatusCodes.Status200OK;
            if (existing.ContentType is not null) context.Response.ContentType = existing.ContentType;
            context.Response.Headers["Idempotent-Replayed"] = "true";
            if (existing.ResponseBody is { Length: > 0 } body) await context.Response.Body.WriteAsync(body, context.RequestAborted);
            return;
        }

        var original = context.Response.Body;
        await using var capture = new MemoryStream();
        context.Response.Body = capture;
        var succeeded = false;
        try
        {
            await next(context);
            succeeded = true;
        }
        finally
        {
            context.Response.Body = original;
            var status = context.Response.StatusCode;
            var store = succeeded && status < 500 && capture.Length <= settings.MaxResponseBytes;
            if (store)
            {
                var bytes = capture.ToArray();
                var contentType = context.Response.ContentType;
                await db.IdempotencyKeys.Where(r => r.Scope == scope && r.Key == key).ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.State, IdempotencyState.Completed)
                    .SetProperty(r => r.StatusCode, status)
                    .SetProperty(r => r.ContentType, contentType)
                    .SetProperty(r => r.ResponseBody, bytes), CancellationToken.None);
            }
            else
            {
                // Let the client retry for real.
                await db.IdempotencyKeys.Where(r => r.Scope == scope && r.Key == key).ExecuteDeleteAsync(CancellationToken.None);
            }
        }

        capture.Position = 0;
        await capture.CopyToAsync(original, context.RequestAborted);
    }

    private static async Task<string> HashRequestAsync(HttpContext context, int maxBytes)
    {
        if (context.Request.ContentLength > maxBytes) throw new ProblemException(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE");
        context.Request.EnableBuffering(maxBytes);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes($"{context.Request.Method}\n{context.Request.Path}{context.Request.QueryString}\n"));
        var buffer = new byte[8192];
        int read;
        long total = 0;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new ProblemException(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE");
            sha.AppendData(buffer.AsSpan(0, read));
        }

        context.Request.Body.Position = 0;
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-:.]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidKey();
}
