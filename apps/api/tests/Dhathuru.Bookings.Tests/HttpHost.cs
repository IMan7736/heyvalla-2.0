using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using Dhathuru.Bookings.Payments;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Tests;

/// <summary>Test-only sign-in: <c>X-Test-User</c> is the user id, <c>X-Test-Unverified</c> drops the verified-email claim.</summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var user) || string.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var claims = new List<Claim> { new("sub", user.ToString()), new("email", $"{user}@example.test") };
        if (!Request.Headers.ContainsKey("X-Test-Unverified"))
        {
            claims.Add(new Claim("email_verified", "true"));
        }
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// The Bookings endpoints on a real Kestrel listener, wired like the Api host: global per-user limiter plus ONLY an
/// "auth" named policy (so a route asking for any other named policy fails exactly as it does in production),
/// authentication, the verified-email policy, and ProblemException answered as problem+json.
/// </summary>
internal sealed class HttpHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private HttpHost(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    public static async Task<HttpHost> StartAsync(Harness h)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var s = builder.Services;
        s.AddSingleton(h.Clock);
        s.AddSingleton<TimeProvider>(h.Clock);
        s.AddSingleton<Dhathuru.Domain.Seams.ITripPlanner>(h.Planner);
        s.AddSingleton<ITransportProvider>(h.Ferry);
        s.AddSingleton<ITransportProvider>(h.Flight);
        s.AddSingleton<IEmailSender>(h.Email);
        s.AddSingleton<Dhathuru.Bookings.Payments.IPaymentGateway>(h.Flaky);
        s.AddBookingsModule(h.Configuration);
        s.RemoveAll<IHostedService>(); // the sweeper has its own tests; here only the HTTP surface is under test
        s.AddAuthentication(TestAuthHandler.Scheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        s.AddAuthorizationBuilder()
            .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Dhathuru.Domain.Security.Policies.VerifiedEmail, p => p.RequireAuthenticatedUser().RequireClaim("email_verified", "true"));
        s.AddRateLimiter(o =>
        {
            o.AddPolicy(RateLimitPolicies.Auth, http => RateLimitPartition.GetNoLimiter("auth"));
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetNoLimiter("all"));
        });

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (ProblemException problem)
            {
                context.Response.Clear();
                context.Response.StatusCode = problem.Status;
                await context.Response.WriteAsJsonAsync(new { code = problem.Code, status = problem.Status, @params = problem.Params, errors = problem.Errors });
            }
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        var v1 = app.MapGroup("/v1");
        foreach (var module in app.Services.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(v1);
        }
        await app.StartAsync();
        var address = app.Urls.First();
        return new HttpHost(app, address);
    }

    public HttpClient Client(string? user = null, bool verified = true)
    {
        var client = new HttpClient { BaseAddress = new Uri(BaseAddress) };
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", user);
            if (!verified)
            {
                client.DefaultRequestHeaders.Add("X-Test-Unverified", "1");
            }
        }
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

internal static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostWithKey(this HttpClient client, string url, object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static async Task<JsonElement> Body(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
