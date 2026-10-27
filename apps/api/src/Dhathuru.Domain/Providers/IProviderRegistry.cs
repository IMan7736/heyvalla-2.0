using Dhathuru.Domain.Transport;

namespace Dhathuru.Domain.Providers;

/// <summary>Finds the adapter for a provider id. Bookings and the status endpoint go through this, never to a concrete class.</summary>
public interface IProviderRegistry
{
    ITransportProvider Get(string providerId);

    /// <summary>The kind configured for a provider (<c>Providers:{id}:Kind</c>, falling back to <c>Providers:Default:Kind</c>, then mock).</summary>
    ProviderKind ConfiguredKind(string providerId);
}
