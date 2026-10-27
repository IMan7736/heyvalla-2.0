using System.Text.Json;
using System.Text.Json.Nodes;
using Dhathuru.Bookings.Domain;
using Dhathuru.Domain.Pricing;

namespace Dhathuru.Bookings.Services;

public sealed record MoneyDto(long AmountMinor, string Currency)
{
    public static MoneyDto Of(long minor, string currency) => new(minor, currency);

    public static MoneyDto Of(Money money) => new(money.AmountMinor, money.Currency);
}

public sealed record PassengerDto(string Id, string? TravellerId, string FullName, string PassengerType, string? Phone);

public sealed record BookingLegDto(string Id, int LegIndex, JsonNode Leg, string Status, string? ProviderReference, string? FailureCode, MoneyDto Amount);

public sealed record ChallengeDto(string Id, string Kind);

public sealed record PaymentDto(string Id, string BookingId, MoneyDto Amount, string Status, string Gateway, string? FailureCode, string CreatedAt, ChallengeDto? Challenge, MoneyDto? Refunded);

public sealed record PriceLineDto(string Code, MoneyDto Amount, int? LegIndex, bool PayOnBoard);

public sealed record DisruptionDto(string Id, string BookingLegId, int LegIndex, string Kind, int DelayMinutes, string State, string DetectedAt, JsonNode? Suggestion);

public sealed record BookingDto(
    string Id,
    string Status,
    string OptionId,
    IReadOnlyList<BookingLegDto> Legs,
    IReadOnlyList<PassengerDto> Passengers,
    MoneyDto Total,
    string? HoldExpiresAt,
    PaymentDto? Payment,
    string CreatedAt,
    string UpdatedAt,
    string? FailureCode,
    IReadOnlyList<PriceLineDto> PriceBreakdown,
    IReadOnlyList<DisruptionDto> Disruptions);

public sealed record TicketDto(string Id, string BookingId, string BookingLegId, string PassengerId, string Status, string IssuedAt, string? UsedAt, JsonNode? Leg);

public sealed record PageInfoDto(string? NextCursor, int Limit);

public sealed record BookingPageDto(IReadOnlyList<BookingDto> Items, PageInfoDto Page);

public sealed record LegQuoteDto(int LegIndex, bool Cancellable, string PolicyCode, int RefundPercent, MoneyDto Refund, string? ReasonCode);

public sealed record CancellationQuoteDto(string BookingId, bool Cancellable, IReadOnlyList<LegQuoteDto> Legs, MoneyDto TotalRefund, MoneyDto NonRefundable, string? ReasonCode);

public static class BookingViews
{
    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public static BookingDto ToDto(Booking booking)
    {
        var legs = booking.Legs.OrderBy(l => l.LegIndex).Select(ToDto).ToList();
        var payment = booking.CurrentPayment;
        return new BookingDto(
            booking.Id,
            WireNames.Of(booking.Status),
            booking.OptionId,
            legs,
            [.. booking.Passengers.Select(p => new PassengerDto(p.Id, p.TravellerId, p.FullName, WireNames.Of(p.Kind), p.Phone))],
            MoneyDto.Of(booking.TotalMinor, booking.Currency),
            booking.HoldExpiresAt is { } h ? Iso(h) : null,
            payment is null ? null : ToDto(payment),
            Iso(booking.CreatedAt),
            Iso(booking.UpdatedAt),
            booking.FailureCode,
            PriceLines(booking),
            [.. booking.Disruptions.OrderBy(d => d.DetectedAt).Select(ToDto)]);
    }

    public static BookingLegDto ToDto(BookingLeg leg) => new(
        leg.Id,
        leg.LegIndex,
        JsonNode.Parse(leg.LegJson)!,
        WireNames.Of(leg.Status),
        leg.ProviderReference,
        leg.FailureCode,
        MoneyDto.Of(leg.AmountMinor, Money.Mvr));

    public static PaymentDto ToDto(PaymentRecord payment) => new(
        payment.Id,
        payment.BookingId,
        MoneyDto.Of(payment.AmountMinor, payment.Currency),
        WireNames.Of(payment.Status),
        payment.Gateway,
        payment.FailureCode,
        Iso(payment.CreatedAt),
        payment.ChallengeId is null ? null : new ChallengeDto(payment.ChallengeId, "verification_code"),
        payment.RefundedMinor > 0 ? MoneyDto.Of(payment.RefundedMinor, payment.Currency) : null);

    public static DisruptionDto ToDto(Disruption d) => new(
        d.Id,
        d.BookingLegId,
        d.LegIndex,
        d.Cancelled ? "cancelled" : "delayed",
        d.DelayMinutes,
        d.State.ToString().ToLowerInvariant(),
        Iso(d.DetectedAt),
        d.SuggestionJson is null ? null : JsonNode.Parse(d.SuggestionJson));

    public static TicketDto ToDto(Ticket ticket, BookingLeg? leg) => new(
        ticket.Id,
        ticket.BookingId,
        ticket.BookingLegId,
        ticket.PassengerId,
        WireNames.Of(ticket.Status),
        Iso(ticket.IssuedAt),
        ticket.UsedAt is { } u ? Iso(u) : null,
        leg is null ? null : JsonNode.Parse(leg.LegJson));

    private static List<PriceLineDto> PriceLines(Booking booking)
    {
        var lines = new List<PriceLineDto>();
        foreach (var node in JsonNode.Parse(booking.PriceLinesJson)!.AsArray())
        {
            var o = node!.AsObject();
            lines.Add(new PriceLineDto(
                o["code"]!.GetValue<string>(),
                new MoneyDto(o["amountMinor"]!.GetValue<long>(), booking.Currency),
                o["legIndex"]?.GetValue<int>(),
                o["payOnBoard"]?.GetValue<bool>() ?? false));
        }
        return lines;
    }

    public static string PriceLinesJson(IEnumerable<(string Code, long AmountMinor, int? LegIndex, bool PayOnBoard)> lines) =>
        JsonSerializer.Serialize(lines.Select(l => new Dictionary<string, object?>
        {
            ["code"] = l.Code,
            ["amountMinor"] = l.AmountMinor,
            ["legIndex"] = l.LegIndex,
            ["payOnBoard"] = l.PayOnBoard,
        }));
}
