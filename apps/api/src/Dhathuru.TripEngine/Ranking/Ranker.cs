using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Assembly;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine.Ranking;

/// <summary>
/// Picks the options worth showing. Tolerance filter, Pareto front over (the time the traveller set, price, transfers, miss risk),
/// a documented 0..100 score, the four labels and a diversity pass so six options are never six near-duplicates.
/// </summary>
internal sealed class Ranker(EngineOptions options)
{
    public sealed record Outcome(IReadOnlyList<TripOption> Options, bool ToleranceRelaxed);

    public Outcome Rank(
        IReadOnlyList<Candidate> all,
        RiskTolerance tolerance,
        bool backward,
        long anchorMinute,
        string snapshotVersion,
        string querySignature)
    {
        if (all.Count == 0) return new Outcome([], false);
        var profile = options.Profile(tolerance);

        foreach (var c in all)
        {
            c.WithinTolerance = c.MissProbability <= profile.MaxMissProbability + 1e-12
                && (c.MinSlackMin == long.MaxValue || c.MinSlackMin >= profile.MinSlackMin);
            c.ElapsedMin = backward ? anchorMinute - c.DepartMin : c.ArriveMin - anchorMinute;
        }

        var tier = all.Where(c => c.WithinTolerance).ToList();
        var relaxed = tier.Count == 0;
        var eligible = relaxed ? all.ToList() : tier;
        var pool = Pareto(eligible, backward);
        if (relaxed)
        {
            foreach (var c in pool)
                c.Warnings.Add(new TripWarning("RISK_ABOVE_TOLERANCE", new Dictionary<string, object?> { ["missProbability"] = Math.Round(c.MissProbability, 3) }));
        }

        var scores = Score(eligible, profile);

        var fastest = pool.OrderBy(c => c.ElapsedMin).ThenBy(c => Math.Round(c.MissProbability, 3)).ThenBy(c => c.TotalMinor).ThenBy(c => c.Signature, StringComparer.Ordinal).First();
        var cheapest = pool.OrderBy(c => c.TotalMinor).ThenBy(c => c.ElapsedMin).ThenBy(c => Math.Round(c.MissProbability, 3)).ThenBy(c => c.Signature, StringComparer.Ordinal).First();
        var safest = pool.OrderBy(c => Math.Round(c.MissProbability, 4)).ThenByDescending(c => c.MinSlackMin).ThenBy(c => c.ElapsedMin).ThenBy(c => c.Signature, StringComparer.Ordinal).First();
        var best = pool.OrderByDescending(c => scores[c]).ThenBy(c => c.MissProbability).ThenBy(c => c.ElapsedMin).ThenBy(c => c.Signature, StringComparer.Ordinal).First();

        var chosen = new List<Candidate>();
        foreach (var w in new[] { best, fastest, cheapest, safest })
        {
            if (!chosen.Contains(w)) chosen.Add(w);
        }
        // Fill with sensible backups: not beaten on score by much, and different from what is already shown.
        var bestElapsed = pool.Min(c => c.ElapsedMin);
        foreach (var c in eligible.OrderByDescending(c => scores[c]).ThenBy(c => c.ElapsedMin).ThenBy(c => c.Signature, StringComparer.Ordinal))
        {
            if (chosen.Count >= options.MaxOptions) break;
            if (chosen.Contains(c) || IsNearDuplicate(c, chosen)) continue;
            if (c.ElapsedMin > bestElapsed + options.AlternativeWindowMin) continue;
            chosen.Add(c);
        }
        chosen = chosen.Take(Math.Max(options.MaxOptions, 4)).ToList();

        var minTransfers = pool.Min(c => c.Transfers);
        var maxTransfers = pool.Max(c => c.Transfers);
        var result = new List<TripOption>();
        foreach (var c in chosen.OrderByDescending(c => scores[c]).ThenBy(c => c.ElapsedMin).ThenBy(c => c.Signature, StringComparer.Ordinal))
        {
            var labels = new List<TripLabel>();
            var why = new List<Explanation>();
            if (c == fastest) { labels.Add(TripLabel.Fastest); why.Add(new Explanation("FASTEST_OVERALL", Empty, Severity.Good)); }
            if (c == cheapest) { labels.Add(TripLabel.Cheapest); why.Add(new Explanation("CHEAPEST_OVERALL", Empty, Severity.Good)); }
            if (c == safest) { labels.Add(TripLabel.Safest); why.Add(new Explanation("SAFEST_BUFFER", Empty, Severity.Good)); }
            if (c == best) labels.Add(TripLabel.BestOverall);
            if (c.Transfers == minTransfers && minTransfers < maxTransfers)
                why.Add(new Explanation("FEWER_TRANSFERS", new Dictionary<string, object?> { ["count"] = c.Transfers }, Severity.Good));

            var explanations = why.Concat(c.Explanations).ToList();
            var risk = new Risk(
                Math.Round(c.MissProbability, 4),
                c.MissProbability < options.Scoring.LowRiskBelow ? RiskLevel.Low
                    : c.MissProbability < options.Scoring.HighRiskFrom ? RiskLevel.Medium : RiskLevel.High,
                c.WeakestConnection);

            result.Add(new TripOption(
                StableIds.Option($"{snapshotVersion}|{querySignature}|{c.Signature}"),
                TimeMath.Instant(c.DepartMin),
                TimeMath.Instant(c.ArriveMin),
                c.Transfers,
                c.Legs,
                c.Price,
                c.Buffers,
                risk,
                labels,
                Math.Round(Math.Clamp(scores[c], 0, 100), 1),
                explanations,
                c.Warnings));
        }
        return new Outcome(result, relaxed);
    }

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private bool IsNearDuplicate(Candidate c, List<Candidate> chosen) =>
        chosen.Any(o => o.ServicePattern == c.ServicePattern
            && o.ScheduledDepartures.Count == c.ScheduledDepartures.Count
            && o.ScheduledDepartures.Zip(c.ScheduledDepartures).All(p => Math.Abs(p.First - p.Second) < options.Scoring.DiversityDepartureMin));

    /// <summary>Keep options no other option beats or ties on every criterion. Risk compared at 0.2 percentage points.</summary>
    private static List<Candidate> Pareto(List<Candidate> items, bool backward)
    {
        var kept = new List<Candidate>();
        foreach (var c in items.OrderBy(c => c.ArriveMin).ThenBy(c => c.TotalMinor).ThenBy(c => c.Signature, StringComparer.Ordinal))
        {
            if (kept.Any(k => Beats(k, c, backward))) continue;
            kept.RemoveAll(k => Beats(c, k, backward));
            kept.Add(c);
        }
        return kept;
    }

    private static bool Beats(Candidate a, Candidate b, bool backward)
    {
        const double riskEps = 0.002;
        // The time criterion is the one the traveller set: arrive early (leaving after a time) or leave late (arriving by a time).
        long ta = backward ? -a.DepartMin : a.ArriveMin, tb = backward ? -b.DepartMin : b.ArriveMin;
        var le = ta <= tb && a.TotalMinor <= b.TotalMinor && a.Transfers <= b.Transfers && a.MissProbability <= b.MissProbability + riskEps;
        if (!le) return false;
        var strict = ta < tb || a.TotalMinor < b.TotalMinor || a.Transfers < b.Transfers || a.MissProbability < b.MissProbability - riskEps;
        // Identical on everything that matters: keep the earlier one in signature order only.
        return strict || string.CompareOrdinal(a.Signature, b.Signature) < 0;
    }

    /// <summary>
    /// score = 100 * (1 - (wT*time + wP*price + wR*risk + wX*transfers)), every term in 0..1 relative to the pool.
    /// time: extra elapsed minutes over the best, saturating at max(best * spread, floor).
    /// price: extra cost relative to the cheapest, saturating at +100 %.
    /// risk: miss probability relative to the saturation point (default 20 %).
    /// transfers: extra connections over the best, saturating at 5.
    /// </summary>
    private Dictionary<Candidate, double> Score(List<Candidate> pool, ToleranceProfile profile)
    {
        var s = options.Scoring;
        var minElapsed = pool.Min(c => c.ElapsedMin);
        var minPrice = pool.Min(c => c.TotalMinor);
        var minTransfers = pool.Min(c => c.Transfers);
        var timeScale = Math.Max(minElapsed * s.TimeSpreadFraction, s.TimeFloorMin);
        var weights = profile.WeightTime + profile.WeightPrice + profile.WeightRisk + profile.WeightTransfers;

        var scores = new Dictionary<Candidate, double>();
        foreach (var c in pool)
        {
            var t = Math.Clamp((c.ElapsedMin - minElapsed) / timeScale, 0, 1);
            var p = Math.Clamp((c.TotalMinor - minPrice) / (double)Math.Max(minPrice, 1), 0, 1);
            var r = Math.Clamp(c.MissProbability / s.RiskSaturation, 0, 1);
            var x = Math.Clamp((c.Transfers - minTransfers) / (double)s.TransferSaturation, 0, 1);
            var penalty = (profile.WeightTime * t + profile.WeightPrice * p + profile.WeightRisk * r + profile.WeightTransfers * x) / weights;
            scores[c] = 100 * (1 - penalty) + s.ReplanKeptLegBonus * c.PreservedLegs;
        }
        return scores;
    }
}
