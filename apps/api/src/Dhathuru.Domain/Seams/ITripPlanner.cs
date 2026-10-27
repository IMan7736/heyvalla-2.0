using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Trips;

namespace Dhathuru.Domain.Seams;

/// <summary>FARU implements; the Api hosts the endpoints. Returns codes and parameters, never English prose.</summary>
public interface ITripPlanner
{
    Task<TripSearchResult> SearchAsync(TripSearchQuery query, CancellationToken ct);

    Task<TripOption?> GetOptionAsync(TripOptionId id, CancellationToken ct);

    Task<TripSearchResult> ReplanAsync(ReplanQuery query, CancellationToken ct);
}
