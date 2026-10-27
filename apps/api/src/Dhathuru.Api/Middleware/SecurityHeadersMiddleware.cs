namespace Dhathuru.Api.Middleware;

/// <summary>
/// Headers for a JSON API: no sniffing, no framing, no referrer, and nothing sensitive cached.
/// Every response is <c>no-store</c> except health, which is harmless and may be polled by load balancers.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            if (!headers.ContainsKey("Cache-Control")) headers["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
