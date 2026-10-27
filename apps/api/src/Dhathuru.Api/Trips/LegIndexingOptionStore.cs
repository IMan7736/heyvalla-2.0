using System.Collections.Concurrent;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine;

namespace Dhathuru.Api.Trips;

/// <summary>
/// Wraps the engine's option store and remembers every leg of every stored option, so <c>GET /geo/legs</c> can resolve
/// a leg id (or geometry ref) to its endpoints. Same bounded, expiring behaviour as the option store.
/// </summary>
public sealed class LegIndexingOptionStore(IOptionStore inner, TimeProvider clock, int capacity = 100_000, TimeSpan? timeToLive = null) : IOptionStore
{
    private readonly TimeSpan ttl = timeToLive ?? TimeSpan.FromHours(3);
    private readonly ConcurrentDictionary<string, (Leg Leg, DateTimeOffset At)> legs = new();
    private readonly ConcurrentQueue<string> order = new();

    public void Put(TripOption option)
    {
        inner.Put(option);
        var now = clock.GetUtcNow();
        foreach (var leg in option.Legs)
        {
            if (legs.TryAdd(leg.Id.Value, (leg, now))) order.Enqueue(leg.Id.Value);
            else legs[leg.Id.Value] = (leg, now);
        }
        while (legs.Count > capacity && order.TryDequeue(out var oldest)) legs.TryRemove(oldest, out _);
    }

    public TripOption? Get(Dhathuru.Domain.Ids.TripOptionId id) => inner.Get(id);

    /// <summary>The leg, or null when unknown or expired. Accepts a leg id or a geometry ref (which equals the leg id).</summary>
    public Leg? FindLeg(string idOrRef)
    {
        if (!legs.TryGetValue(idOrRef, out var hit)) return null;
        if (clock.GetUtcNow() - hit.At <= ttl) return hit.Leg;
        legs.TryRemove(idOrRef, out _);
        return null;
    }
}
