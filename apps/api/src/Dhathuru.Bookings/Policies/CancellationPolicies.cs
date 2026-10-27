using System.Reflection;
using System.Text.Json;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Bookings.Policies;

/// <summary>One step of a refund ladder: at least <c>MinHoursBeforeDeparture</c> before departure, refund <c>RefundPercent</c> of the leg fare.</summary>
public sealed record RefundTier(int MinHoursBeforeDeparture, int RefundPercent);

/// <summary>Cancellation rules for one provider or mode, loaded from data (<c>cancellation-policies.json</c>), not code.</summary>
public sealed record CancellationPolicy(string Code, IReadOnlyList<RefundTier> Tiers, long NonRefundableFeeMinor = 0);

public sealed record LegRefundQuote(int LegIndex, bool Cancellable, string PolicyCode, int RefundPercent, Money Refund, string? ReasonCode);

public interface ICancellationPolicyCatalog
{
    CancellationPolicy For(string providerId, TransportMode mode);
}

public sealed class CancellationPolicyCatalog : ICancellationPolicyCatalog
{
    private sealed record FileModel(Dictionary<string, PolicyModel> Modes, Dictionary<string, PolicyModel> Providers);

    private sealed record PolicyModel(string Code, List<RefundTier> Tiers, long NonRefundableFeeMinor = 0);

    private readonly Dictionary<string, CancellationPolicy> _byMode;
    private readonly Dictionary<string, CancellationPolicy> _byProvider;

    public CancellationPolicyCatalog()
    {
        using var stream = typeof(CancellationPolicyCatalog).Assembly
            .GetManifestResourceStream("Dhathuru.Bookings.Policies.cancellation-policies.json")
            ?? throw new InvalidOperationException("cancellation-policies.json is not embedded.");
        var model = JsonSerializer.Deserialize<FileModel>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("cancellation-policies.json is empty.");
        _byMode = model.Modes.ToDictionary(p => p.Key, p => Map(p.Value), StringComparer.OrdinalIgnoreCase);
        _byProvider = model.Providers.ToDictionary(p => p.Key, p => Map(p.Value), StringComparer.OrdinalIgnoreCase);
    }

    public CancellationPolicy For(string providerId, TransportMode mode) =>
        _byProvider.TryGetValue(providerId, out var provider)
            ? provider
            : _byMode.TryGetValue(ModeKey(mode), out var byMode) ? byMode : _byMode["default"];

    private static CancellationPolicy Map(PolicyModel m) =>
        new(m.Code, [.. m.Tiers.OrderByDescending(t => t.MinHoursBeforeDeparture)], m.NonRefundableFeeMinor);

    private static string ModeKey(TransportMode mode) => mode switch
    {
        TransportMode.DomesticFlight => "domestic_flight",
        TransportMode.ResortTransfer => "resort_transfer",
        _ => mode.ToString().ToLowerInvariant(),
    };
}

public static class RefundCalculator
{
    /// <summary>
    /// Refund for cancelling a leg at <paramref name="now"/>. A departed leg is never cancellable.
    /// The refund never exceeds what was paid and never goes negative.
    /// </summary>
    public static LegRefundQuote Quote(int legIndex, CancellationPolicy policy, long paidMinor, DateTimeOffset departAt, DateTimeOffset now)
    {
        if (departAt <= now)
        {
            return new LegRefundQuote(legIndex, false, policy.Code, 0, Money.Zero, "LEG_DEPARTED");
        }

        var hours = (departAt - now).TotalHours;
        var tier = policy.Tiers.FirstOrDefault(t => hours >= t.MinHoursBeforeDeparture);
        var percent = tier?.RefundPercent ?? 0;
        var gross = paidMinor * percent / 100;
        var refund = Math.Max(0, Math.Min(paidMinor, gross - (percent > 0 ? policy.NonRefundableFeeMinor : 0)));
        return new LegRefundQuote(legIndex, true, policy.Code, percent, new Money(refund), percent == 0 ? "NO_REFUND_WINDOW" : null);
    }
}
