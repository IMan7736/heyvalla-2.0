namespace Dhathuru.Domain.Ids;

/// <summary>Identifier of a place. The prefix tells the kind: isl, atl, apt, jty, txs, poi or adr.</summary>
public readonly record struct PlaceId
{
    private static readonly string[] Prefixes = ["isl", "atl", "apt", "jty", "txs", "poi", "adr"];

    private PlaceId(string value) => Value = value;

    public string Value { get; }

    public static string PrefixFor(Places.PlaceType type) => type switch
    {
        Places.PlaceType.Island => "isl",
        Places.PlaceType.Atoll => "atl",
        Places.PlaceType.Airport => "apt",
        Places.PlaceType.Jetty => "jty",
        Places.PlaceType.TaxiStand => "txs",
        Places.PlaceType.Poi => "poi",
        Places.PlaceType.Address => "adr",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static PlaceId New(Places.PlaceType type) => new($"{PrefixFor(type)}_{Ulid.NewString()}");

    public static PlaceId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid place id: '{value}'.");

    public static bool TryParse(string? value, out PlaceId id)
    {
        if (value is { Length: 30 } && value[3] == '_' && Array.IndexOf(Prefixes, value[..3]) >= 0 && Ulid.IsValid(value.AsSpan(4)))
        {
            id = new PlaceId(value);
            return true;
        }
        id = default;
        return false;
    }

    public Places.PlaceType Type => Value[..3] switch
    {
        "isl" => Places.PlaceType.Island,
        "atl" => Places.PlaceType.Atoll,
        "apt" => Places.PlaceType.Airport,
        "jty" => Places.PlaceType.Jetty,
        "txs" => Places.PlaceType.TaxiStand,
        "poi" => Places.PlaceType.Poi,
        _ => Places.PlaceType.Address,
    };

    public override string ToString() => Value ?? string.Empty;
}
