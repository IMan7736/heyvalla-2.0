using System.Net;
using System.Threading.RateLimiting;
using Dhathuru.Api.Email;
using Dhathuru.Api.Middleware;
using Dhathuru.Api.Persistence;
using Dhathuru.Domain.Configuration;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Security;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dhathuru.Api.Infrastructure;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Requests per window per client address on credential and email endpoints.</summary>
    public int AuthPermits { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>Requests per window per signed-in user (or client address when anonymous) on everything else.</summary>
    public int DefaultPermits { get; set; } = 300;

    public int DefaultWindowSeconds { get; set; } = 60;
}

public static class PlatformServices
{
    /// <summary>Everything the host adds around the modules: storage, email, limits, headers, telemetry.</summary>
    public static IServiceCollection AddDhathuruPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        // Audit trail and idempotency keys.
        services.AddDbContextFactory<PlatformDbContext>(o => o.UseNpgsql(
            configuration.GetDatabaseOptions().ToConnectionString(),
            npgsql => npgsql.MigrationsHistoryTable(PlatformDbContext.MigrationsTable, PlatformDbContext.Schema)));
        services.AddSingleton<IAuditLog, AuditLog>();
        services.AddSingleton<IModuleMigrator, PlatformMigrator>();
        services.AddHostedService<MigrationHostedService>();
        services.Configure<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.SectionName));

        // Email: modules enqueue through IEmailSender; a background service delivers.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<QueuedEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<QueuedEmailSender>());
        services.AddSingleton<IEmailTransport>(sp =>
            string.Equals(configuration[$"{EmailOptions.SectionName}:Provider"], "log", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<LogEmailTransport>(sp)
                : ActivatorUtilities.CreateInstance<SmtpEmailTransport>(sp));
        services.AddHostedService<EmailDeliveryService>();

        services.Configure<BffOptions>(configuration.GetSection(BffOptions.SectionName));
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.AddDhathuruRateLimiting(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // ASP.NET trusts loopback by default, which lets any local caller choose its own client address (L-03).
            // Trust exactly what configuration lists.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            // Only a trusted proxy (the Next.js BFF, the platform load balancer) may set the client address.
            foreach (var proxy in configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in configuration.GetSection("Proxy:KnownNetworks").Get<string[]>() ?? [])
            {
                var parts = network.Split('/');
                options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse(parts[0]), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
            }

            // With nothing listed the middleware would trust every caller, so switch it off instead (L-03).
            if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
            }
        });

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
        });

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("dhathuru-api"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/v1/health"))
                    .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) t.AddOtlpExporter();
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
                if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) m.AddOtlpExporter();
            });
        return services;
    }

    private static IServiceCollection AddDhathuruRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                // Sliding-window leases carry no retry hint, so fall back to one segment of the window: the soonest a
                // permit can free up. Always a positive number (L-14).
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? (int)Math.Ceiling(retry.TotalSeconds)
                    : (int)Math.Ceiling(settings.DefaultWindowSeconds / 6.0);
                retryAfter = Math.Max(1, retryAfter);
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Extensions = { ["params"] = new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfter } },
                    },
                });
            };

            options.AddPolicy(RateLimitPolicies.Auth, http =>
                settings.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter($"auth:{Client(http)}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermits,
                        Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                        QueueLimit = 0,
                    })
                    : RateLimitPartition.GetNoLimiter("auth"));

            // Named so any module can opt in with RequireRateLimiting("default"); counts separately from the global limiter.
            options.AddPolicy(RateLimitPolicies.Default, http =>
                settings.Enabled
                    ? RateLimitPartition.GetSlidingWindowLimiter(
                        $"policy-default:{http.User.FindFirst("sub")?.Value ?? Client(http)}",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = settings.DefaultPermits,
                            Window = TimeSpan.FromSeconds(settings.DefaultWindowSeconds),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("default"));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                settings.Enabled && !http.Request.Path.StartsWithSegments("/v1/health")
                    ? RateLimitPartition.GetSlidingWindowLimiter(
                        $"default:{http.User.FindFirst("sub")?.Value ?? Client(http)}",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = settings.DefaultPermits,
                            Window = TimeSpan.FromSeconds(settings.DefaultWindowSeconds),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("exempt"));
        });
        return services;
    }

    private static string Client(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
