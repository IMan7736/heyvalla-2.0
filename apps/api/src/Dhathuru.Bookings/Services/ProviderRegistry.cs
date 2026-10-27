using Dhathuru.Domain.Providers;

namespace Dhathuru.Bookings.Services;

/// <summary>Finds the adapter for a provider id. Static, mock and live adapters are all registered as <see cref="ITransportProvider"/>.</summary>
public sealed class ProviderRegistry(IEnumerable<ITransportProvider> providers)
{
    private readonly Dictionary<string, ITransportProvider> _byId = providers.ToDictionary(p => p.ProviderId, StringComparer.Ordinal);

    public ITransportProvider? Find(string providerId) => _byId.GetValueOrDefault(providerId);

    public IReadOnlyCollection<ITransportProvider> All => _byId.Values;
}
