using System.Globalization;
using System.Text.Json.Serialization;
using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;

namespace Dhathuru.Api.Trips;

// ---- requests (shapes from packages/contracts/openapi/trips.yaml) ----------------------------------------------------

public sealed record MoneyBody(long AmountMinor, string? Currency);

public sealed record PassengersBody(int Adults, int Children = 0, int Infants = 0);

public sealed record PreferencesBody(string? RiskTolerance, int? MaxTransfers = null, MoneyBody? BudgetMax = null, string[]? Modes = null);

public sealed record TripSearchBody(
    string? From, string? To, string? Date, string? Time, string? TimeMode, PassengersBody? Passengers, PreferencesBody? Preferences);

public sealed record DisruptionBody(string? LegId, string? Kind, int? DelayMinutes = null);

public sealed record ReplanBody(string? BookingId, DateTimeOffset? Now, DisruptionBody? Disruption);

/// <summary>Turns request bodies into domain queries, or a 422 whose field errors carry stable codes.</summary>
public static class TripRequests
{
    public const int MaxPartySize = 9;
    public const int MaxDaysAhead = 365;

    public static TripSearchQuery ToQuery(TripSearchBody? body, DateTimeOffset now)
    {
        var errors = new List<FieldError>();
        if (body is null) throw new ProblemException(StatusCodes.Status400BadRequest, "BAD_REQUEST");

        PlaceId from = default, to = default;
        if (!PlaceId.TryParse(body.From, out from)) errors.Add(new FieldError("from", "INVALID_PLACE_ID"));
        if (!PlaceId.TryParse(body.To, out to)) errors.Add(new FieldError("to", "INVALID_PLACE_ID"));

        var today = MaldivesTime.LocalDate(now);
        if (!DateOnly.TryParseExact(body.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            errors.Add(new FieldError("date", "INVALID_DATE"));
        else if (date < today)
            errors.Add(new FieldError("date", "DATE_IN_PAST"));
        else if (date > today.AddDays(MaxDaysAhead))
            errors.Add(new FieldError("date", "DATE_TOO_FAR", new Dictionary<string, object?> { ["maxDays"] = MaxDaysAhead }));

        if (!TimeOnly.TryParseExact(body.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            errors.Add(new FieldError("time", "INVALID_TIME"));

        TimeMode timeMode = default;
        switch (body.TimeMode)
        {
            case "departAfter": timeMode = TimeMode.DepartAfter; break;
            case "arriveBy": timeMode = TimeMode.ArriveBy; break;
            default: errors.Add(new FieldError("timeMode", "INVALID_TIME_MODE")); break;
        }

        var party = body.Passengers;
        if (party is null) errors.Add(new FieldError("passengers", "REQUIRED"));
        else
        {
            if (party.Adults < 1) errors.Add(new FieldError("passengers.adults", "AT_LEAST_ONE_ADULT"));
            if (party.Children < 0 || party.Infants < 0) errors.Add(new FieldError("passengers", "NEGATIVE_COUNT"));
            else if (party.Infants > party.Adults)
                errors.Add(new FieldError("passengers.infants", "INFANTS_EXCEED_ADULTS", new Dictionary<string, object?> { ["adults"] = party.Adults, ["infants"] = party.Infants }));
            else if (party.Adults + party.Children + party.Infants > MaxPartySize)
                errors.Add(new FieldError("passengers", "PARTY_TOO_LARGE", new Dictionary<string, object?> { ["max"] = MaxPartySize }));
        }

        var prefs = body.Preferences;
        var risk = RiskTolerance.Balanced;
        if (prefs is null) errors.Add(new FieldError("preferences", "REQUIRED"));
        else
        {
            switch (prefs.RiskTolerance)
            {
                case "cautious": risk = RiskTolerance.Cautious; break;
                case "balanced": risk = RiskTolerance.Balanced; break;
                case "relaxed": risk = RiskTolerance.Relaxed; break;
                default: errors.Add(new FieldError("preferences.riskTolerance", "INVALID_RISK_TOLERANCE")); break;
            }
            if (prefs.MaxTransfers is < 0 or > 8)
                errors.Add(new FieldError("preferences.maxTransfers", "OUT_OF_RANGE", new Dictionary<string, object?> { ["min"] = 0, ["max"] = 8 }));
            if (prefs.BudgetMax is { AmountMinor: < 0 } or { Currency: not null and not Money.Mvr })
                errors.Add(new FieldError("preferences.budgetMax", "INVALID_MONEY"));
        }

        List<TransportMode>? modes = null;
        if (prefs?.Modes is { } wire)
        {
            modes = [];
            foreach (var name in wire)
            {
                try { modes.Add(WireNames.ParseMode(name)); }
                catch (FormatException) { errors.Add(new FieldError("preferences.modes", "INVALID_MODE")); break; }
            }
        }

        if (errors.Count > 0) throw ProblemException.Validation([.. errors]);

        return new TripSearchQuery(
            from, to, date, time, timeMode,
            new Passengers(party!.Adults, party.Children, party.Infants),
            new TripPreferences(
                risk,
                prefs!.MaxTransfers,
                prefs.BudgetMax is { } budget ? new Money(budget.AmountMinor) : null,
                modes is { Count: > 0 } ? modes : null));
    }

    public static ReplanQuery ToReplan(ReplanBody? body)
    {
        var errors = new List<FieldError>();
        if (body is null) throw new ProblemException(StatusCodes.Status400BadRequest, "BAD_REQUEST");

        if (!BookingId.TryParse(body.BookingId, out var booking)) errors.Add(new FieldError("bookingId", "INVALID_BOOKING_ID"));
        if (body.Now is null) errors.Add(new FieldError("now", "REQUIRED"));

        LegId leg = default;
        var kind = DisruptionKind.Delayed;
        int? delay = null;
        if (body.Disruption is null) errors.Add(new FieldError("disruption", "REQUIRED"));
        else
        {
            if (!LegId.TryParse(body.Disruption.LegId, out leg)) errors.Add(new FieldError("disruption.legId", "INVALID_LEG_ID"));
            switch (body.Disruption.Kind)
            {
                case "delayed":
                    kind = DisruptionKind.Delayed;
                    if (body.Disruption.DelayMinutes is null or < 1) errors.Add(new FieldError("disruption.delayMinutes", "DELAY_REQUIRED"));
                    else delay = body.Disruption.DelayMinutes;
                    break;
                case "cancelled": kind = DisruptionKind.Cancelled; break;
                default: errors.Add(new FieldError("disruption.kind", "INVALID_DISRUPTION_KIND")); break;
            }
        }

        if (errors.Count > 0) throw ProblemException.Validation([.. errors]);
        return new ReplanQuery(booking, body.Now!.Value, new Disruption(leg, kind, delay));
    }
}

// ---- responses -------------------------------------------------------------------------------------------------------

public sealed record GeoWire(double Lat, double Lng);

public sealed record PlaceRefWire(
    string Id, string Type, IReadOnlyDictionary<string, string> Name, GeoWire Geo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code);

public sealed record MoneyWire(long AmountMinor, string Currency)
{
    public static MoneyWire From(Money m) => new(m.AmountMinor, m.Currency);
}

public sealed record LegWire(
    string Id, string Mode, string ProviderId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServiceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServiceRunId,
    PlaceRefWire From, PlaceRefWire To, DateTimeOffset DepartAt, DateTimeOffset ArriveAt, int DurationMin,
    MoneyWire Price, bool Bookable, string Pricing,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? GeometryRef,
    IReadOnlyList<string> NoteCodes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OperatorName);

public sealed record BreakdownWire(string Code, MoneyWire Amount, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LegId);

public sealed record PriceWire(MoneyWire Total, IReadOnlyList<BreakdownWire> Breakdown);

public sealed record BufferWire(int AfterLegIndex, int Minutes, int RequiredMinutes, int ComfortMinutes);

public sealed record RiskWire(double MissProbability, string Level, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? WeakestConnectionIndex);

public sealed record ExplanationWire(string Code, IReadOnlyDictionary<string, object?> Params, string Severity);

public sealed record WarningWire(string Code, IReadOnlyDictionary<string, object?> Params);

public sealed record TripOptionWire(
    string Id, DateTimeOffset DepartAt, DateTimeOffset ArriveAt, int DurationMin, int Transfers,
    IReadOnlyList<LegWire> Legs, PriceWire Price, IReadOnlyList<BufferWire> Buffers, RiskWire Risk,
    IReadOnlyList<string> Labels, double Score, IReadOnlyList<ExplanationWire> Explanations, IReadOnlyList<WarningWire> Warnings);

public sealed record TripSearchWire(string SearchId, IReadOnlyList<TripOptionWire> Options, IReadOnlyList<ExplanationWire> Notices);

public static class TripWire
{
    public static TripSearchWire From(TripSearchResult result, IReadOnlyList<Explanation> extraNotices) =>
        new(result.SearchId.Value, [.. result.Options.Select(From)], [.. result.NoticeList.Concat(extraNotices).Select(From)]);

    public static TripOptionWire From(TripOption o) => new(
        o.Id.Value, o.DepartAt, o.ArriveAt, o.DurationMin, o.Transfers,
        [.. o.Legs.Select(From)],
        new PriceWire(MoneyWire.From(o.Price.Total), [.. o.Price.Breakdown.Select(b => new BreakdownWire(b.Code, MoneyWire.From(b.Amount), b.LegId?.Value))]),
        [.. o.Buffers.Select(b => new BufferWire(b.AfterLegIndex, b.Minutes, b.RequiredMinutes, b.ComfortMinutes))],
        new RiskWire(o.Risk.MissProbability, Level(o.Risk.Level), o.Risk.WeakestConnectionIndex),
        [.. o.Labels.Select(Label)], o.Score,
        [.. o.Explanations.Select(From)],
        [.. o.Warnings.Select(w => new WarningWire(w.Code, w.Params))]);

    public static LegWire From(Leg l) => new(
        l.Id.Value, WireNames.ToWire(l.Mode), l.ProviderId, l.ServiceId?.Value, l.ServiceRunId,
        Ref(l.From), Ref(l.To), l.DepartAt, l.ArriveAt, l.DurationMin, MoneyWire.From(l.Price), l.Bookable,
        l.Pricing == PricingKind.Fixed ? "fixed" : "estimate", l.GeometryRef, l.NoteCodes, l.OperatorName);

    private static ExplanationWire From(Explanation e) =>
        new(e.Code, e.Params, e.Severity switch { Severity.Good => "good", Severity.Caution => "caution", _ => "info" });

    private static PlaceRefWire Ref(Dhathuru.Domain.Places.PlaceRef p) =>
        new(p.Id.Value, WireNames.ToWire(p.Type), p.Name, new GeoWire(p.Geo.Lat, p.Geo.Lng), p.Code);

    private static string Level(RiskLevel level) => level switch { RiskLevel.Medium => "medium", RiskLevel.High => "high", _ => "low" };

    private static string Label(TripLabel label) => label switch
    {
        TripLabel.Fastest => "fastest",
        TripLabel.Cheapest => "cheapest",
        TripLabel.Safest => "safest",
        _ => "best_overall",
    };
}
