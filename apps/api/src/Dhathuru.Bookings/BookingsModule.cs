using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Policies;
using Dhathuru.Bookings.Services;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dhathuru.Bookings;

public static class BookingsModule
{
    /// <summary>
    /// Registers the Bookings module. The host must also register <c>ITripPlanner</c>, <c>IEmailSender</c> and at least one
    /// <c>ITransportProvider</c>, and call <see cref="Endpoints.BookingEndpoints.MapBookingsEndpoints"/> on its <c>/v1</c> group.
    /// </summary>
    public static IServiceCollection AddBookingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily so the host can start (and answer /health) before a password is configured.
        services.AddDbContext<BookingsDbContext>(o => Configure(o, configuration.GetDatabaseOptions().ToConnectionString()));

        services.Configure<BookingsOptions>(configuration.GetSection(BookingsOptions.Section));
        services.Configure<TicketSigningOptions>(configuration.GetSection(TicketSigningOptions.Section));
        services.Configure<VerificationOptions>(configuration.GetSection(VerificationOptions.Section));
        services.Configure<FakeGatewayOptions>(configuration.GetSection($"{BookingsOptions.Section}:FakeGateway"));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPaymentGateway, FakeGateway>();
        services.TryAddSingleton<ICancellationPolicyCatalog, CancellationPolicyCatalog>();
        services.TryAddSingleton<TicketSigner>();
        services.TryAddSingleton<ProviderRegistry>();
        services.TryAddSingleton<IConfirmFaultInjector>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BookingsOptions>>().Value.EnableSimulation
                ? new FakeTokenFaultInjector()
                : new NoFaultInjector());

        services.AddSingleton<Dhathuru.Domain.Http.IEndpointModule, Endpoints.BookingsEndpointModule>();
        services.AddSingleton<Dhathuru.Domain.Seams.IModuleMigrator, BookingsMigrator>();
        services.AddScoped<Dhathuru.Domain.Seams.IAccountDeletionHandler, BookingsAccountDeletion>();
        services.TryAddSingleton<Dhathuru.Domain.Seams.IBookedItineraryProvider, BookedItineraryProvider>();
        services.AddHttpContextAccessor();
        services.AddScoped<BookingNotifier>();
        services.AddScoped<BookingService>();
        services.AddScoped<TicketService>();
        services.AddScoped<DisruptionService>();
        services.AddHostedService<ProductionSafetyCheck>();
        services.AddHostedService<HoldSweeper>();
        services.AddHostedService<DisruptionMonitor>();
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(BookingsDbContext.MigrationsTable, BookingsDbContext.Schema));
}

/// <summary>Lets <c>dotnet ef migrations add --project src/Dhathuru.Bookings</c> work without the host.</summary>
public sealed class BookingsDesignTimeFactory : IDesignTimeDbContextFactory<BookingsDbContext>
{
    public BookingsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var builder = new DbContextOptionsBuilder<BookingsDbContext>();
        BookingsModule.Configure(builder, configuration.GetDatabaseOptions().ToConnectionString());
        return new BookingsDbContext(builder.Options);
    }
}
