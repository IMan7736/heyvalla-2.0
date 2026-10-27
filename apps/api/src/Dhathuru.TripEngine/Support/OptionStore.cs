using System.Collections.Concurrent;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Trips;

namespace Dhathuru.TripEngine;

/// <summary>Remembers options returned by search so <c>GET /trips/options/{id}</c> and booking can read them back as they were.</summary>
public interface IOptionStore
{
    void Put(TripOption option);

    TripOption? Get(TripOptionId id);
}

/// <summary>Bounded in-memory store: oldest entries leave first, entries expire after the time to live.</summary>
public sealed class InMemoryOptionStore(TimeProvider? clock = null, int capacity = 20_000, TimeSpan? timeToLive = null) : IOptionStore
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly TimeSpan ttl = timeToLive ?? TimeSpan.FromHours(3);
    private readonly ConcurrentDictionary<string, (TripOption Option, DateTimeOffset At)> items = new();
    private readonly ConcurrentQueue<string> order = new();

    public void Put(TripOption option)
    {
        if (items.TryAdd(option.Id.Value, (option, clock.GetUtcNow()))) order.Enqueue(option.Id.Value);
        else items[option.Id.Value] = (option, clock.GetUtcNow());
        while (items.Count > capacity && order.TryDequeue(out var oldest)) items.TryRemove(oldest, out _);
    }

    public TripOption? Get(TripOptionId id)
    {
        if (!items.TryGetValue(id.Value, out var hit)) return null;
        if (clock.GetUtcNow() - hit.At <= ttl) return hit.Option;
        items.TryRemove(id.Value, out _);
        return null;
    }
}
