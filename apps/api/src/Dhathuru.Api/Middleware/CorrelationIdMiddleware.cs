using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Dhathuru.Api.Middleware;

/// <summary>
/// Gives every request a correlation id: the caller's <c>X-Correlation-Id</c> when it looks sane, otherwise a new one.
/// It is echoed in the response, added to every log line, tagged on the trace and included in problem details.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "correlation-id";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var id = Valid().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = id;
        context.Response.Headers[HeaderName] = id;
        Activity.Current?.SetTag("correlation.id", id);
        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    [GeneratedRegex(@"^[A-Za-z0-9\-_.]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Valid();
}

internal static class ClientAddress
{
    /// <summary>Client address reduced to a /24 (IPv4) or /48 (IPv6) prefix.</summary>
    public static string? Truncated(HttpContext? context)
    {
        var address = context?.Connection.RemoteIpAddress;
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4) return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
        for (var i = 6; i < bytes.Length; i++) bytes[i] = 0;
        return new IPAddress(bytes) + "/48";
    }
}
