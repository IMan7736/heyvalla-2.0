using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Seams;

namespace Dhathuru.Catalog.Services;

/// <summary>
/// Reliability per service. No dataset we hold contains scheduled-versus-actual times, so every figure here is a
/// per-mode prior with <c>Samples == 0</c>. Consumers must treat Samples == 0 as "synthetic prior, not observed".
/// Unknown services return null: "no history", never "perfect".
/// </summary>
public sealed class CatalogReliabilityStats(CatalogCache cache) : IReliabilityStats
{
    public async Task<ReliabilityStat?> GetAsync(ServiceId serviceId, CancellationToken ct)
    {
        var index = await cache.GetAsync(ct);
        if (!index.ServicesById.TryGetValue(serviceId, out var service)) return null;
        var prior = index.Data.ReliabilityPriors.FirstOrDefault(p => p.Mode == service.Mode);
        return prior is null ? null : new ReliabilityStat(serviceId, prior.OnTimeRate, prior.P90DelayMin, prior.Samples);
    }
}
