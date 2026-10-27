using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine;

namespace Dhathuru.Api.Trips;

/// <summary>
/// Hosts FARU's trip engine: <c>POST /trips/search</c>, <c>GET /trips/options/{id}</c>, <c>POST /trips/replan</c> and
/// KANDU's illustrative <c>GET /geo/legs</c>. Errors are codes; the engine's notices travel in <c>notices</c>.
/// </summary>
public static class TripsEndpoints
{
    public const int MaxGeoLegs = 20;

    public static RouteGroupBuilder MapTripsEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/trips/search", SearchAsync).WithName("searchTrips").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Default);
        group.MapGet("/trips/options/{optionId}", GetOptionAsync).WithName("getTripOption").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Default);
        group.MapPost("/trips/replan", ReplanAsync).WithName("replanTrip").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Default);
        group.MapGet("/geo/legs", GetLegGeometriesAsync).WithName("getLegGeometries").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Default);
        return group;
    }

    private static async Task<IResult> SearchAsync(TripSearchBody? body, ITripPlanner planner, TimeProvider time, CancellationToken ct)
    {
        var query = TripRequests.ToQuery(body, time.GetUtcNow());
        // PlanAsync also reports why a search is empty; any other planner still returns Notices on the result.
        TripSearchResult result;
        IReadOnlyList<Explanation> notices = [];
        if (planner is TripPlanner engine)
        {
            var plan = await engine.PlanAsync(query, ct);
            (result, notices) = (plan.Result, plan.Notices);
        }
        else result = await planner.SearchAsync(query, ct);
        return Results.Ok(TripWire.From(result, notices));
    }

    private static async Task<IResult> GetOptionAsync(string optionId, ITripPlanner planner, CancellationToken ct)
    {
        if (!TripOptionId.TryParse(optionId, out var id)) throw ProblemException.NotFound("OPTION_NOT_FOUND");
        var option = await planner.GetOptionAsync(id, ct) ?? throw ProblemException.NotFound("OPTION_NOT_FOUND");
        return Results.Ok(TripWire.From(option));
    }

    private static async Task<IResult> ReplanAsync(ReplanBody? body, ITripPlanner planner, CancellationToken ct)
    {
        var query = TripRequests.ToReplan(body);
        TripSearchResult result;
        IReadOnlyList<Explanation> notices = [];
        if (planner is TripPlanner engine)
        {
            var plan = await engine.PlanReplanAsync(query, ct);
            (result, notices) = (plan.Result, plan.Notices);
        }
        else result = await planner.ReplanAsync(query, ct);
        if (result.Options.Count == 0 && notices.Any(n => n.Code == "BOOKING_NOT_FOUND"))
            throw ProblemException.NotFound("BOOKING_NOT_FOUND");
        return Results.Ok(TripWire.From(result, notices));
    }

    private static IResult GetLegGeometriesAsync(string? ids, LegIndexingOptionStore legs)
    {
        var requested = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (requested.Count == 0)
            throw ProblemException.Validation(new FieldError("ids", "REQUIRED"));
        if (requested.Count > MaxGeoLegs)
            throw ProblemException.Validation(new FieldError("ids", "TOO_MANY", new Dictionary<string, object?> { ["max"] = MaxGeoLegs }));

        var features = new List<object>();
        foreach (var id in requested)
        {
            var leg = legs.FindLeg(id) ?? throw ProblemException.NotFound("LEG_NOT_FOUND");
            var line = RouteGeometry.Build(leg.From.Geo, leg.To.Geo, leg.Mode);
            features.Add(new
            {
                type = "Feature",
                properties = new { legId = leg.Id.Value, mode = WireNames.ToWire(leg.Mode), approximate = line.Approximate },
                geometry = new { type = "LineString", coordinates = line.Coordinates },
            });
        }
        return Results.Json(new { type = "FeatureCollection", features }, contentType: "application/geo+json");
    }
}
