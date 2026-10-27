using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Security;
using Dhathuru.Domain.Seams;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;
using Serilog.Events;

namespace Dhathuru.Api.Tests.Support;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    public IReadOnlyList<EmailMessage> Sent => [.. sent];

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage Last(string to, string template) =>
        sent.Reverse().First(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.TemplateCode == template);

    public int Count(string to, string template) =>
        sent.Count(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.TemplateCode == template);

    /// <summary>The single-use token from the link in an email.</summary>
    public string TokenFor(string to, string template)
    {
        var link = Last(to, template).Data["link"];
        return Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
    }
}

public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> lines = new();

    public IReadOnlyList<string> Lines => [.. lines];

    private readonly ConcurrentQueue<string> errors = new();

    /// <summary>Rendered Error and Fatal events, with the exception type when there is one.</summary>
    public IReadOnlyList<string> Errors => [.. errors];

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level >= LogEventLevel.Error) errors.Enqueue($"{logEvent.RenderMessage()} {logEvent.Exception?.GetType().Name}");
        Capture(logEvent);
    }

    private void Capture(LogEvent logEvent) => lines.Enqueue(logEvent.RenderMessage() + " " + string.Join(' ', logEvent.Properties.Select(p => p.Value.ToString())));
}

/// <summary>Counts calls so idempotency tests can prove a retry did not run the endpoint again.</summary>
public sealed class EchoCounter
{
    public int Calls;
}

internal sealed class TestEndpoints(EchoCounter counter) : IEndpointModule
{
    public void MapEndpoints(RouteGroupBuilder v1)
    {
        v1.MapPost("/_test/echo", (JsonElement body) =>
            {
                var n = Interlocked.Increment(ref counter.Calls);
                return Results.Json(new { n, body });
            })
            .RequireAuthorization(Policies.Authenticated)
            .RequireIdempotency();
        v1.MapPost("/_test/fail", () =>
            {
                Interlocked.Increment(ref counter.Calls);
                return Results.Problem(statusCode: 500);
            })
            .RequireAuthorization(Policies.Authenticated)
            .RequireIdempotency();
        v1.MapGet("/_test/verified", () => Results.Ok()).RequireAuthorization(Policies.VerifiedEmail);
        v1.MapGet("/_test/admin", () => Results.Ok()).RequireAuthorization(Policies.Admin);
        v1.MapGet("/_test/boom", () => Task.FromException<IResult>(new InvalidOperationException("secret detail"))).AllowAnonymous();
    }
}

/// <summary>The real API host against a real Postgres, with email captured and rate limiting off unless asked for.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture database;

    public PostgresFixture Database => database;
    private readonly Dictionary<string, string?> settings;

    public ApiFactory(PostgresFixture database, Dictionary<string, string?>? overrides = null)
    {
        this.database = database;
        settings = new Dictionary<string, string?>
        {
            ["Database:Host"] = database.Host,
            ["Database:Port"] = database.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Database:Name"] = "dhathuru_test",
            ["Database:Username"] = "dhathuru",
            ["Database:Password"] = "test-only-password",
            ["Database:MigrateOnStartup"] = "true",
            ["Auth:ConstantTimeFloorMilliseconds"] = "0",
            ["Auth:EmailCooldownSeconds"] = "0",
            ["RateLimiting:Enabled"] = "false",
        };
        foreach (var (key, value) in overrides ?? []) settings[key] = value;
    }

    public CapturingEmailSender Emails { get; } = new();

    public CapturingLogSink Logs { get; } = new();

    public EchoCounter Counter { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<ILogEventSink>(Logs);
            services.AddSingleton(Counter);
            services.AddSingleton<IEndpointModule, TestEndpoints>();
        });
    }

    public HttpClient Anonymous() => CreateClient();

    public HttpClient As(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}

public sealed record Tokens(string AccessToken, string RefreshToken);

public sealed record TestUser(string Email, string Password, string Id, Tokens Tokens);

public static class ApiFactoryExtensions
{
    public const string StrongPassword = "correct horse battery staple";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static async Task<TestUser> RegisterAndLoginAsync(this ApiFactory api, string? email = null, string password = StrongPassword, bool verify = false)
    {
        email ??= NewEmail();
        var register = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email, password, displayName = "Test" });
        register.EnsureSuccessStatusCode();
        if (verify)
        {
            var verified = await api.Anonymous().PostAsJsonAsync("/v1/auth/verify-email", new { token = api.Emails.TokenFor(email, "account.verify_email") });
            verified.EnsureSuccessStatusCode();
        }

        var tokens = await api.LoginAsync(email, password);
        var me = await api.As(tokens.AccessToken).GetFromJsonAsync<JsonElement>("/v1/me");
        return new TestUser(email, password, me.GetProperty("id").GetString()!, tokens);
    }

    public static async Task<Tokens> LoginAsync(this ApiFactory api, string email, string password)
    {
        var response = await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Tokens(body.GetProperty("accessToken").GetString()!, body.GetProperty("refreshToken").GetString()!);
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string> CodeAsync(this HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("code").GetString()!;
}
