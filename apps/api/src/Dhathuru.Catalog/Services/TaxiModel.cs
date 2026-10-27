using Dhathuru.Domain.Pricing;

namespace Dhathuru.Catalog.Data;

public enum EstimateConfidence { High, Medium, Low }

public enum EstimateBasis
{
    /// <summary>Fare read from the operator's zone matrix; duration from at least 10 observed rides.</summary>
    Observed,

    /// <summary>Fare from the zone matrix, duration from the distance fallback.</summary>
    MatrixFare,

    /// <summary>Everything from the distance fallback formula. A guess.</summary>
    Fallback,
}

public sealed record TaxiEstimate(Money Fare, int DurationMin, int DurationP90Min, EstimateConfidence Confidence, EstimateBasis Basis);

/// <summary>Observed duration for a zone pair in a time-of-day band. Built by DataTools from aggregated rides (n &gt;= 10).</summary>
public sealed record TaxiDurationStat(string FromZone, string ToZone, string Band, int Samples, double MedianMin, double P90Min, double MedianKm);

public sealed record TaxiFare(string FromZone, string ToZone, long FareMinor);

/// <summary>Least squares fit: <c>value = Intercept + PerKm * km</c>. R2 says how much to trust it.</summary>
public sealed record LinearFit(double Intercept, double PerKm, double R2, int Samples);

public sealed record TaxiModel(
    IReadOnlyList<TaxiFare> Fares,
    IReadOnlyList<TaxiDurationStat> Durations,
    LinearFit DurationFallbackMin,
    LinearFit FareFallbackMvr,
    IReadOnlyDictionary<string, (int FromHour, int ToHour)> Bands)
{
    /// <summary>Road distance is longer than the straight line; derived from rides in DataTools (median ratio), this default is a labelled guess.</summary>
    public const double DefaultDetourFactor = 1.3;

    public const int MinSamples = 10;

    public string BandFor(int localHour)
    {
        foreach (var (name, (from, to)) in Bands)
        {
            if (localHour >= from && localHour <= to) return name;
        }
        return Bands.Keys.First();
    }

    /// <summary>Estimate between two taxi zones, or between arbitrary points when a zone key is missing.</summary>
    public TaxiEstimate Estimate(string? fromZone, string? toZone, double straightLineKm, int localHour)
    {
        var band = BandFor(localHour);
        var fare = fromZone is null || toZone is null ? null : Fares.FirstOrDefault(f => f.FromZone == fromZone && f.ToZone == toZone);
        var observed = fromZone is null || toZone is null
            ? null
            : Durations.FirstOrDefault(d => d.FromZone == fromZone && d.ToZone == toZone && d.Band == band && d.Samples >= MinSamples)
              ?? Durations.Where(d => d.FromZone == fromZone && d.ToZone == toZone && d.Samples >= MinSamples)
                  .OrderByDescending(d => d.Samples).FirstOrDefault();

        var roadKm = straightLineKm * DefaultDetourFactor;
        var fallbackMin = Math.Max(1, DurationFallbackMin.Intercept + DurationFallbackMin.PerKm * roadKm);
        var fallbackFare = Money.FromMvr((long)Math.Max(0, Math.Round(FareFallbackMvr.Intercept + FareFallbackMvr.PerKm * roadKm)));

        if (fare is not null && observed is not null)
        {
            return new TaxiEstimate(new Money(fare.FareMinor), (int)Math.Ceiling(observed.MedianMin), (int)Math.Ceiling(observed.P90Min),
                observed.Samples >= 100 ? EstimateConfidence.High : EstimateConfidence.Medium, EstimateBasis.Observed);
        }
        if (fare is not null)
        {
            var d = (int)Math.Ceiling(fallbackMin);
            return new TaxiEstimate(new Money(fare.FareMinor), d, (int)Math.Ceiling(d * 1.5), EstimateConfidence.Medium, EstimateBasis.MatrixFare);
        }
        var minutes = (int)Math.Ceiling(fallbackMin);
        return new TaxiEstimate(fallbackFare, minutes, (int)Math.Ceiling(minutes * 1.5), EstimateConfidence.Low, EstimateBasis.Fallback);
    }
}
