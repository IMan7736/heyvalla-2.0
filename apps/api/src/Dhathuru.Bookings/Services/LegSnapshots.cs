using System.Text.Json.Nodes;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// The booked leg is stored as a JSON snapshot in the contract's Leg shape, so what the traveller bought never
/// changes if the planner later re-prices or removes the option.
/// </summary>
public static class LegSnapshots
{
    public static string ModeWire(TransportMode mode) => mode switch
    {
        TransportMode.DomesticFlight => "domestic_flight",
        TransportMode.ResortTransfer => "resort_transfer",
        _ => mode.ToString().ToLowerInvariant(),
    };

    public static bool TryParseMode(string? wire, out TransportMode mode)
    {
        foreach (var candidate in Enum.GetValues<TransportMode>())
        {
            if (ModeWire(candidate) == wire)
            {
                mode = candidate;
                return true;
            }
        }
        mode = default;
        return false;
    }

    public static string PlaceTypeWire(PlaceType type) => type switch
    {
        PlaceType.TaxiStand => "taxi_stand",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static JsonObject ToJson(Leg leg)
    {
        var node = new JsonObject
        {
            ["id"] = leg.Id.Value,
            ["mode"] = ModeWire(leg.Mode),
            ["providerId"] = leg.ProviderId,
            ["from"] = Place(leg.From),
            ["to"] = Place(leg.To),
            ["departAt"] = leg.DepartAt.ToUniversalTime().ToString("O"),
            ["arriveAt"] = leg.ArriveAt.ToUniversalTime().ToString("O"),
            ["durationMin"] = leg.DurationMin,
            ["price"] = MoneyNode(leg.Price),
            ["bookable"] = leg.Bookable,
            ["pricing"] = leg.Pricing == PricingKind.Fixed ? "fixed" : "estimate",
            ["noteCodes"] = new JsonArray([.. leg.NoteCodes.Select(c => (JsonNode?)JsonValue.Create(c))]),
        };
        if (leg.ServiceId is { } serviceId)
        {
            node["serviceId"] = serviceId.Value;
        }
        if (leg.GeometryRef is not null)
        {
            node["geometryRef"] = leg.GeometryRef;
        }
        return node;
    }

    public static JsonObject MoneyNode(Money money) => new() { ["amountMinor"] = money.AmountMinor, ["currency"] = money.Currency };

    private static JsonObject Place(PlaceRef place)
    {
        var node = new JsonObject
        {
            ["id"] = place.Id.Value,
            ["type"] = PlaceTypeWire(place.Type),
            ["name"] = new JsonObject([.. place.Name.Select(n => KeyValuePair.Create<string, JsonNode?>(n.Key, n.Value))]),
            ["geo"] = new JsonObject { ["lat"] = place.Geo.Lat, ["lng"] = place.Geo.Lng },
        };
        if (place.Code is not null)
        {
            node["code"] = place.Code;
        }
        return node;
    }

    /// <summary>Rebuilds the planner leg from a booked snapshot. The service run is stored beside the snapshot, not in it.</summary>
    public static Leg FromJson(JsonObject node, string? serviceRunId)
    {
        var mode = TryParseMode(node["mode"]!.GetValue<string>(), out var parsed) ? parsed : throw new FormatException("Unknown mode in leg snapshot.");
        var price = node["price"]!.AsObject();
        return new Leg(
            LegId.Parse(node["id"]!.GetValue<string>()),
            mode,
            node["providerId"]!.GetValue<string>(),
            PlaceFrom(node["from"]!.AsObject()),
            PlaceFrom(node["to"]!.AsObject()),
            DepartAt(node),
            ArriveAt(node),
            new Money(price["amountMinor"]!.GetValue<long>(), price["currency"]!.GetValue<string>()),
            node["bookable"]!.GetValue<bool>(),
            node["pricing"]!.GetValue<string>() == "fixed" ? PricingKind.Fixed : PricingKind.Estimate,
            [.. (node["noteCodes"]?.AsArray() ?? []).Select(c => c!.GetValue<string>())],
            node["serviceId"] is { } sid ? ServiceId.Parse(sid.GetValue<string>()) : null,
            serviceRunId,
            node["geometryRef"]?.GetValue<string>());
    }

    private static PlaceRef PlaceFrom(JsonObject node)
    {
        var type = Enum.GetValues<PlaceType>().First(t => PlaceTypeWire(t) == node["type"]!.GetValue<string>());
        var names = node["name"]!.AsObject().ToDictionary(n => n.Key, n => n.Value!.GetValue<string>());
        var geo = node["geo"]!.AsObject();
        return new PlaceRef(PlaceId.Parse(node["id"]!.GetValue<string>()), type, names,
            new GeoPoint(geo["lat"]!.GetValue<double>(), geo["lng"]!.GetValue<double>()), node["code"]?.GetValue<string>());
    }

    public static DateTimeOffset DepartAt(JsonObject leg) => DateTimeOffset.Parse(leg["departAt"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);

    public static DateTimeOffset ArriveAt(JsonObject leg) => DateTimeOffset.Parse(leg["arriveAt"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);

    public static string PlaceName(JsonObject leg, string side, string locale = "en")
    {
        var name = leg[side]?["name"]?.AsObject();
        return name?[locale]?.GetValue<string>() ?? name?["en"]?.GetValue<string>() ?? "";
    }
}
