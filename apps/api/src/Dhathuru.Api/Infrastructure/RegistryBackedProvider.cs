using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;

namespace Dhathuru.Api.Infrastructure;

/// <summary>
/// One <see cref="ITransportProvider"/> per provider id for modules that look providers up by id (Bookings). It forwards to
/// KANDU's <see cref="IProviderRegistry"/> on every call, so the configured kind (static, mock, live) is honoured and a
/// configuration change never needs a new wiring. Provider ids come from <c>Providers:Ids</c> plus any registered live factory.
/// </summary>
public sealed class RegistryBackedProvider(string providerId, IProviderRegistry registry) : ITransportProvider
{
    public string ProviderId { get; } = providerId;

    public ProviderKind Kind => registry.ConfiguredKind(ProviderId);

    private ITransportProvider Inner => registry.Get(ProviderId);

    public Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct) => Inner.GetDeparturesAsync(q, ct);

    public Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) => Inner.GetAvailabilityAsync(q, ct);

    public Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct) => Inner.HoldAsync(r, ct);

    public Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct) => Inner.ConfirmAsync(r, ct);

    public Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct) => Inner.CancelAsync(r, ct);

    public Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct) => Inner.GetStatusAsync(serviceRunId, ct);
}

public static class ProviderWiring
{
    /// <summary>Registers an <see cref="ITransportProvider"/> for every configured provider id.</summary>
    public static IServiceCollection AddConfiguredProviders(this IServiceCollection services, IConfiguration configuration)
    {
        var ids = (configuration.GetSection("Providers:Ids").Get<string[]>() ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            services.AddSingleton<ITransportProvider>(sp => new RegistryBackedProvider(id, sp.GetRequiredService<IProviderRegistry>()));
        }
        return services;
    }
}
