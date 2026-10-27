using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dhathuru.Api.Middleware;

public sealed class BffOptions
{
    public const string SectionName = "Bff";

    /// <summary>
    /// Secret shared with the Next.js BFF (<c>BFF_SHARED_KEY</c> there, <c>Bff__SharedKey</c> here). Only a request that
    /// presents it may say which end user it is acting for. Unset means nobody is trusted to say, which is safe.
    /// </summary>
    public string? SharedKey { get; set; }
}

/// <summary>
/// Gives rate limits and audit hints the real end user's address when the call comes from our own BFF, and only then.
/// The BFF sends <c>X-Dhathuru-Client-Ip</c> plus <c>X-Dhathuru-Bff-Key</c>; anything else (a browser calling the API,
/// a spoofed <c>X-Forwarded-For</c>) is keyed by the connection itself. Runs after the forwarded-headers middleware, which
/// already ignores untrusted peers.
/// </summary>
public sealed class BffClientAddressMiddleware(RequestDelegate next, IOptions<BffOptions> options)
{
    public const string ClientIpHeader = "X-Dhathuru-Client-Ip";
    public const string KeyHeader = "X-Dhathuru-Bff-Key";

    private readonly byte[]? expected = string.IsNullOrEmpty(options.Value.SharedKey) ? null : Encoding.UTF8.GetBytes(options.Value.SharedKey);

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (expected is not null
            && headers.TryGetValue(KeyHeader, out var key)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key.ToString()), expected)
            && IPAddress.TryParse(headers[ClientIpHeader].ToString(), out var client))
        {
            context.Connection.RemoteIpAddress = client;
        }

        return next(context);
    }
}
