// One struct per id kind; the prefix is part of the value (opr_01K8...).
namespace Dhathuru.Domain.Ids;

/// <summary>Shared parsing rules for prefixed ULIDs.</summary>
internal static class PrefixedUlid
{
    public static string Create(string prefix) => $"{prefix}_{Ulid.NewString()}";

    public static bool IsValid(string? value, string prefix) =>
        value is not null
        && value.Length == prefix.Length + 1 + Ulid.Length
        && value.StartsWith(prefix, StringComparison.Ordinal)
        && value[prefix.Length] == '_'
        && Ulid.IsValid(value.AsSpan(prefix.Length + 1));
}

/// <summary>Identifier with prefix <c>opr_</c>.</summary>
public readonly record struct OperatorId
{
    public const string Prefix = "opr";

    private OperatorId(string value) => Value = value;

    public string Value { get; }

    public static OperatorId New() => new(PrefixedUlid.Create(Prefix));

    public static OperatorId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out OperatorId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new OperatorId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>svc_</c>.</summary>
public readonly record struct ServiceId
{
    public const string Prefix = "svc";

    private ServiceId(string value) => Value = value;

    public string Value { get; }

    public static ServiceId New() => new(PrefixedUlid.Create(Prefix));

    public static ServiceId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out ServiceId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new ServiceId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>trp_</c>.</summary>
public readonly record struct TripOptionId
{
    public const string Prefix = "trp";

    private TripOptionId(string value) => Value = value;

    public string Value { get; }

    public static TripOptionId New() => new(PrefixedUlid.Create(Prefix));

    public static TripOptionId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out TripOptionId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new TripOptionId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>srch_</c>.</summary>
public readonly record struct SearchId
{
    public const string Prefix = "srch";

    private SearchId(string value) => Value = value;

    public string Value { get; }

    public static SearchId New() => new(PrefixedUlid.Create(Prefix));

    public static SearchId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out SearchId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new SearchId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>leg_</c>.</summary>
public readonly record struct LegId
{
    public const string Prefix = "leg";

    private LegId(string value) => Value = value;

    public string Value { get; }

    public static LegId New() => new(PrefixedUlid.Create(Prefix));

    public static LegId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out LegId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new LegId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>bkg_</c>.</summary>
public readonly record struct BookingId
{
    public const string Prefix = "bkg";

    private BookingId(string value) => Value = value;

    public string Value { get; }

    public static BookingId New() => new(PrefixedUlid.Create(Prefix));

    public static BookingId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out BookingId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new BookingId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>bkl_</c>.</summary>
public readonly record struct BookingLegId
{
    public const string Prefix = "bkl";

    private BookingLegId(string value) => Value = value;

    public string Value { get; }

    public static BookingLegId New() => new(PrefixedUlid.Create(Prefix));

    public static BookingLegId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out BookingLegId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new BookingLegId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>tkt_</c>.</summary>
public readonly record struct TicketId
{
    public const string Prefix = "tkt";

    private TicketId(string value) => Value = value;

    public string Value { get; }

    public static TicketId New() => new(PrefixedUlid.Create(Prefix));

    public static TicketId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out TicketId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new TicketId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>pay_</c>.</summary>
public readonly record struct PaymentId
{
    public const string Prefix = "pay";

    private PaymentId(string value) => Value = value;

    public string Value { get; }

    public static PaymentId New() => new(PrefixedUlid.Create(Prefix));

    public static PaymentId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out PaymentId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new PaymentId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>usr_</c>.</summary>
public readonly record struct UserId
{
    public const string Prefix = "usr";

    private UserId(string value) => Value = value;

    public string Value { get; }

    public static UserId New() => new(PrefixedUlid.Create(Prefix));

    public static UserId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out UserId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new UserId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>trv_</c>.</summary>
public readonly record struct TravellerId
{
    public const string Prefix = "trv";

    private TravellerId(string value) => Value = value;

    public string Value { get; }

    public static TravellerId New() => new(PrefixedUlid.Create(Prefix));

    public static TravellerId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out TravellerId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new TravellerId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifier with prefix <c>pax_</c>.</summary>
public readonly record struct PassengerId
{
    public const string Prefix = "pax";

    private PassengerId(string value) => Value = value;

    public string Value { get; }

    public static PassengerId New() => new(PrefixedUlid.Create(Prefix));

    public static PassengerId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"Not a valid {Prefix}_ id: '{value}'.");

    public static bool TryParse(string? value, out PassengerId id)
    {
        if (PrefixedUlid.IsValid(value, Prefix))
        {
            id = new PassengerId(value!);
            return true;
        }
        id = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;
}
