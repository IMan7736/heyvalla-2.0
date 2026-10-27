using Dhathuru.Domain.Http;
using Dhathuru.Integrations.Providers;
using Dhathuru.Integrations.Status;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dhathuru.Domain.Providers;

namespace Dhathuru.Integrations;

/// <summary>
/// Registers provider adapters. Which implementation serves a provider is configuration:
/// <c>Providers:Default:Kind</c> (static | mock | live, default mock) and <c>Providers:{providerId}:Kind</c>.
/// Requires <c>AddCatalogModule</c> (the timetable comes from the catalog).
/// </summary>
public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<TimetableDirectory>();
        services.AddSingleton<DisruptionBoard>();
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        services.AddSingleton<ServiceStatusService>();
        services.AddSingleton<IEndpointModule, StatusEndpointModule>();
        return services;
    }
}
