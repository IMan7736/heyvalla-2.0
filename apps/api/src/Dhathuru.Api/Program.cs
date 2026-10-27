using Dhathuru.Api.Endpoints;
using Dhathuru.Api.Infrastructure;
using Dhathuru.Api.Middleware;
using Dhathuru.Api.Trips;
using Dhathuru.Bookings;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Seams;
using Dhathuru.TripEngine;
using Dhathuru.Catalog;
using Dhathuru.Identity;
using Dhathuru.Identity.Endpoints;
using Dhathuru.Integrations;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddEphemeralBookingKeys();

    builder.Host.UseSerilog(preserveStaticLogger: true, configureLogger: (context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.With<PiiRedactionEnricher>()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}"));

    builder.Services.AddDhathuruProblemDetails();
    builder.Services.AddOpenApi();
    builder.Services.AddSingleton<IDatabaseProbe, NpgsqlDatabaseProbe>();
    builder.Services.AddDhathuruPlatform(builder.Configuration);

    // Modules. Each owns one DbContext, one schema and one migration history table.
    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddCatalogModule(builder.Configuration);
    // FARU's engine, hosted here. The option store is wrapped so GET /geo/legs can resolve legs of returned options.
    builder.Services.TryAddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<LegIndexingOptionStore>(sp =>
        new LegIndexingOptionStore(new InMemoryOptionStore(sp.GetRequiredService<TimeProvider>()), sp.GetRequiredService<TimeProvider>()));
    builder.Services.AddSingleton<IOptionStore>(sp => sp.GetRequiredService<LegIndexingOptionStore>());
    builder.Services.AddSingleton<ITripPlanner, TripPlanner>();
    builder.Services.AddBookingsModule(builder.Configuration);
    builder.Services.AddIntegrationsModule(builder.Configuration);
    builder.Services.AddConfiguredProviders(builder.Configuration); // Bookings finds providers by id

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseMiddleware<BffClientAddressMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostic, http) =>
    {
        diagnostic.Set("UserId", http.User.FindFirst("sub")?.Value);
    });
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseResponseCompression();

    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<IdempotencyMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    var v1 = app.MapGroup("/v1");
    v1.MapHealthEndpoints();
    v1.MapIdentityEndpoints();
    v1.MapTripsEndpoints();
    foreach (var module in app.Services.GetServices<IEndpointModule>())
    {
        module.MapEndpoints(v1);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Dhathuru.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so WebApplicationFactory can host the API in tests.</summary>
public partial class Program;
