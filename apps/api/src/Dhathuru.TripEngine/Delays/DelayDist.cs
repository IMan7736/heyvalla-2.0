using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Dhathuru.TripEngine.Configuration;

namespace Dhathuru.TripEngine.Delays;

/// <summary>
/// Delay of one leg: zero with probability 1 - q, otherwise exponential with mean m minutes.
/// P(D &gt; x) = q * exp(-x / m). Two such delays add (a late ferry plus a slow taxi) with the closed forms below.
/// </summary>
internal readonly record struct DelayDist(double LateProbability, double MeanLateMin)
{
    public static readonly DelayDist None = new(0, 0);

    public bool IsNone => LateProbability <= 0 || MeanLateMin <= 0;

    /// <summary>P(D &gt; x). A negative x is a connection that is already lost.</summary>
    public double Exceeds(double x)
    {
        if (IsNone) return x < 0 ? 1 : 0;
        if (x < 0) return 1;
        return LateProbability * Math.Exp(-x / MeanLateMin);
    }

    /// <summary>Smallest x with P(D &gt; x) &lt;= tail (the p-quantile used for the comfortable buffer).</summary>
    public double Quantile(double tail)
    {
        if (IsNone || LateProbability <= tail) return 0;
        return MeanLateMin * Math.Log(LateProbability / tail);
    }

    /// <summary>
    /// Merge flexible legs that sit between the same pair of scheduled legs into one distribution.
    /// Late if any is late; mean weighted by each leg's late probability. A documented approximation.
    /// </summary>
    public static DelayDist Merge(DelayDist a, DelayDist b)
    {
        if (a.IsNone) return b;
        if (b.IsNone) return a;
        var q = 1 - (1 - a.LateProbability) * (1 - b.LateProbability);
        var mean = (a.LateProbability * a.MeanLateMin + b.LateProbability * b.MeanLateMin)
            / (a.LateProbability + b.LateProbability);
        return new DelayDist(q, mean);
    }

    /// <summary>P(A + B &gt; x) for independent delays.</summary>
    public static double SumExceeds(DelayDist a, DelayDist b, double x)
    {
        if (x < 0) return 1;
        if (a.IsNone) return b.Exceeds(x);
        if (b.IsNone) return a.Exceeds(x);
        var onlyA = a.LateProbability * (1 - b.LateProbability) * Math.Exp(-x / a.MeanLateMin);
        var onlyB = b.LateProbability * (1 - a.LateProbability) * Math.Exp(-x / b.MeanLateMin);
        var both = a.LateProbability * b.LateProbability * HypoExceeds(a.MeanLateMin, b.MeanLateMin, x);
        return Math.Min(1, onlyA + onlyB + both);
    }

    private static double HypoExceeds(double ma, double mb, double x)
    {
        if (Math.Abs(ma - mb) < 1e-9) return Math.Exp(-x / ma) * (1 + x / ma);
        return (ma * Math.Exp(-x / ma) - mb * Math.Exp(-x / mb)) / (ma - mb);
    }
}

/// <summary>Resolves a leg's delay distribution from history when it exists, else from priors. Never treats "no history" as "perfect".</summary>
internal sealed class DelayModel
{
    private readonly DelayPriorOptions priors;
    private readonly IReadOnlyDictionary<string, ReliabilityStat?> stats;

    public DelayModel(DelayPriorOptions priors, IReadOnlyDictionary<string, ReliabilityStat?>? stats = null)
    {
        this.priors = priors;
        this.stats = stats ?? new Dictionary<string, ReliabilityStat?>();
    }

    public DelayPrior Prior(TransportMode mode) => mode switch
    {
        TransportMode.Ferry => priors.Ferry,
        TransportMode.Speedboat => priors.Speedboat,
        TransportMode.Seaplane => priors.Seaplane,
        TransportMode.DomesticFlight => priors.DomesticFlight,
        TransportMode.Bus => priors.Bus,
        TransportMode.ResortTransfer => priors.ResortTransfer,
        TransportMode.Taxi => priors.Taxi,
        _ => new DelayPrior(0, 0),
    };

    public DelayDist ForRun(TransportMode mode, string serviceId)
    {
        var prior = Prior(mode);
        if (!stats.TryGetValue(serviceId, out var stat) || stat is null || stat.Samples <= 0)
            return new DelayDist(prior.LateProbability, prior.MeanLateMin);

        // Shrink observed late rate toward the prior; weight grows with sample count.
        var w = stat.Samples / (double)(stat.Samples + priors.ShrinkageSamples);
        var observedLate = Math.Clamp(1 - stat.OnTimeRate, 0, 1);
        var q = w * observedLate + (1 - w) * prior.LateProbability;

        // P90 delay is the 90th percentile over all runs. With late share q_obs > 10 % it pins the exponential mean:
        // q_obs * exp(-p90/m) = 0.10. Otherwise the history says nothing about the tail and the prior mean stands.
        var mean = prior.MeanLateMin;
        if (observedLate > 0.10 && stat.P90DelayMin > 0)
        {
            var observedMean = stat.P90DelayMin / Math.Log(observedLate / 0.10);
            mean = w * observedMean + (1 - w) * prior.MeanLateMin;
        }
        return new DelayDist(q, Math.Max(1, mean));
    }

    /// <summary>Taxi and other flexible rides: noise proportional to ride length, with a prior floor.</summary>
    public DelayDist ForFlexible(TransportMode mode, int durationMin)
    {
        if (mode == TransportMode.Walk) return DelayDist.None;
        var prior = Prior(mode);
        return new DelayDist(prior.LateProbability, Math.Max(prior.MeanLateMin, priors.TaxiNoiseShareOfDuration * durationMin));
    }

    public ReliabilityStat? StatFor(string serviceId) =>
        stats.TryGetValue(serviceId, out var s) ? s : null;
}
