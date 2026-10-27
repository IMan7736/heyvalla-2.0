using System.Globalization;
using System.Text;
using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dhathuru.Catalog.Endpoints;

public static class CatalogEndpoints
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>Maps GET /places, /places/{id}, /operators and /services onto the given (already versioned) group.</summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/places", SearchPlacesAsync).WithName("searchPlaces").AllowAnonymous();
        group.MapGet("/places/{placeId}", GetPlaceAsync).WithName("getPlace").AllowAnonymous();
        group.MapGet("/operators", ListOperatorsAsync).WithName("listOperators").AllowAnonymous();
        group.MapGet("/services", ListServicesAsync).WithName("listServices").AllowAnonymous();
        return group;
    }

    internal static async Task<IResult> SearchPlacesAsync(
        CatalogCache cache, string? query, string? type, string? near, string? cursor, int? limit, CancellationToken ct)
    {
        var errors = new List<FieldError>();
        PlaceType? typeFilter = null;
        if (type is not null)
        {
            if (WireNames.TryParsePlaceType(type, out var parsed)) typeFilter = parsed;
            else errors.Add(new FieldError("type", "INVALID_PLACE_TYPE"));
        }
        GeoPoint? nearPoint = null;
        if (near is not null)
        {
            if (TryParseNear(near, out var point)) nearPoint = point;
            else errors.Add(new FieldError("near", "INVALID_COORDINATES"));
        }
        if (query is { Length: > 80 }) errors.Add(new FieldError("query", "TOO_LONG", new Dictionary<string, object?> { ["max"] = 80 }));
        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit) errors.Add(new FieldError("limit", "OUT_OF_RANGE", new Dictionary<string, object?> { ["min"] = 1, ["max"] = MaxLimit }));
        var offset = 0;
        if (cursor is not null && !TryDecodeCursor(cursor, out offset)) errors.Add(new FieldError("cursor", "INVALID_CURSOR"));
        if (errors.Count > 0) throw ProblemException.Validation([.. errors]);

        var index = await cache.GetAsync(ct);
        var matches = PlaceSearch.Search(index.Data.Places, query, typeFilter, nearPoint);
        // With a location and no text, nearest first.
        if (string.IsNullOrWhiteSpace(query) && nearPoint is not null)
            matches = [.. matches.OrderBy(m => m.DistanceKm)];
        var page = matches.Skip(offset).Take(take).Select(m => PlaceDto.From(m.Place)).ToList();
        var next = offset + take < matches.Count ? EncodeCursor(offset + take) : null;
        return Results.Ok(new PlacePageDto(page, new PageInfoDto(next, take)));
    }

    internal static async Task<IResult> GetPlaceAsync(CatalogCache cache, string placeId, CancellationToken ct)
    {
        if (!PlaceId.TryParse(placeId, out var id)) throw ProblemException.NotFound("PLACE_NOT_FOUND");
        var index = await cache.GetAsync(ct);
        return index.PlacesById.TryGetValue(id, out var place)
            ? Results.Ok(PlaceDto.From(place))
            : throw ProblemException.NotFound("PLACE_NOT_FOUND");
    }

    internal static async Task<IResult> ListOperatorsAsync(CatalogCache cache, CancellationToken ct)
    {
        var index = await cache.GetAsync(ct);
        return Results.Ok(new OperatorListDto([.. index.Data.Operators.Select(OperatorDto.From)]));
    }

    internal static async Task<IResult> ListServicesAsync(CatalogCache cache, string? from, string? to, string? date, CancellationToken ct)
    {
        var errors = new List<FieldError>();
        if (!PlaceId.TryParse(from, out var fromId)) errors.Add(new FieldError("from", "INVALID_PLACE_ID"));
        if (!PlaceId.TryParse(to, out var toId)) errors.Add(new FieldError("to", "INVALID_PLACE_ID"));
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            errors.Add(new FieldError("date", "INVALID_DATE"));
        if (errors.Count > 0) throw ProblemException.Validation([.. errors]);

        var index = await cache.GetAsync(ct);
        var items = new List<ServiceDto>();
        foreach (var s in index.Data.Services)
        {
            if (s.OnDemand || s.From != fromId || s.To != toId) continue;
            if (!index.PlacesById.TryGetValue(s.From.Value, out var f) || !index.PlacesById.TryGetValue(s.To.Value, out var t)) continue;
            var departures = ServiceCalendar.RunsFor(s, day).Select(r => new DepartureDto(r.ServiceRunId, r.DepartAt, r.ArriveAt)).ToList();
            items.Add(new ServiceDto(
                s.Id.Value, s.OperatorId.Value, WireNames.ToWire(s.Mode), "scheduled", PlaceRefDto.From(f), PlaceRefDto.From(t), departures,
                new ValidityDto(s.ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), s.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), s.DaysOfWeek),
                new FareDto(MoneyDto.From(s.Fare), WireNames.ToWire(s.Pricing), s.ChildDiscountPercent), s.CheckInCutoffMin, s.Synthetic));
        }
        return Results.Ok(new ServiceListDto(items));
    }

    internal static bool TryParseNear(string value, out GeoPoint point)
    {
        point = default;
        var parts = value.Split(',');
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
            || lat is < -90 or > 90 || lng is < -180 or > 180)
            return false;
        point = new GeoPoint(lat, lng);
        return true;
    }

    internal static string EncodeCursor(int offset) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"o:{offset}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static bool TryDecodeCursor(string cursor, out int offset)
    {
        offset = 0;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return text.StartsWith("o:", StringComparison.Ordinal)
                   && int.TryParse(text.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out offset);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Hooks the catalog routes into the API host (it maps every registered <see cref="IEndpointModule"/> under /v1).</summary>
public sealed class CatalogEndpointModule : IEndpointModule
{
    public void MapEndpoints(RouteGroupBuilder v1) => v1.MapCatalogEndpoints();
}
