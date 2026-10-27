using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Data;

/// <summary>snake_case names used in JSON, the database and the OpenAPI contract.</summary>
public static class WireNames
{
    public static string ToWire(TransportMode mode) => mode switch
    {
        TransportMode.Ferry => "ferry",
        TransportMode.Speedboat => "speedboat",
        TransportMode.Seaplane => "seaplane",
        TransportMode.DomesticFlight => "domestic_flight",
        TransportMode.Bus => "bus",
        TransportMode.Taxi => "taxi",
        TransportMode.Walk => "walk",
        TransportMode.ResortTransfer => "resort_transfer",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static TransportMode ParseMode(string value) =>
        Enum.GetValues<TransportMode>().Where(m => ToWire(m) == value).Select(m => (TransportMode?)m).FirstOrDefault()
        ?? throw new FormatException($"Unknown transport mode '{value}'.");

    public static string ToWire(PlaceType type) => type switch
    {
        PlaceType.Island => "island",
        PlaceType.Atoll => "atoll",
        PlaceType.Airport => "airport",
        PlaceType.Jetty => "jetty",
        PlaceType.TaxiStand => "taxi_stand",
        PlaceType.Poi => "poi",
        PlaceType.Address => "address",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static PlaceType ParsePlaceType(string value) =>
        TryParsePlaceType(value, out var type) ? type : throw new FormatException($"Unknown place type '{value}'.");

    public static bool TryParsePlaceType(string? value, out PlaceType type)
    {
        foreach (var t in Enum.GetValues<PlaceType>())
        {
            if (ToWire(t) == value)
            {
                type = t;
                return true;
            }
        }
        type = default;
        return false;
    }

    public static string ToWire(ProviderKind kind) => kind.ToString().ToLowerInvariant();

    public static ProviderKind ParseProviderKind(string value) =>
        Enum.TryParse<ProviderKind>(value, ignoreCase: true, out var kind)
            ? kind
            : throw new FormatException($"Unknown provider kind '{value}'.");

    public static string ToWire(PricingKind pricing) => pricing.ToString().ToLowerInvariant();

    public static PricingKind ParsePricing(string value) =>
        Enum.TryParse<PricingKind>(value, ignoreCase: true, out var kind)
            ? kind
            : throw new FormatException($"Unknown pricing kind '{value}'.");
}
