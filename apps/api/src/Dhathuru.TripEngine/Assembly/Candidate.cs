using Dhathuru.Domain.Trips;
using Buffer = Dhathuru.Domain.Trips.Buffer;
using Dhathuru.TripEngine.Network;

namespace Dhathuru.TripEngine.Assembly;

/// <summary>A fully timed, validated itinerary with everything the ranker needs. Never contains a hard-floor violation.</summary>
internal sealed class Candidate
{
    public required IReadOnlyList<Hop> Hops { get; init; }

    public required IReadOnlyList<Leg> Legs { get; init; }

    public required IReadOnlyList<Buffer> Buffers { get; init; }

    public required TripPrice Price { get; init; }

    public required long TotalMinor { get; init; }

    public required double MissProbability { get; init; }

    public int? WeakestConnection { get; init; }

    /// <summary>Smallest slack (minutes beyond the hard floor) over every risk-bearing connection; <see cref="long.MaxValue"/> if none.</summary>
    public required long MinSlackMin { get; init; }

    public required long DepartMin { get; init; }

    public required long ArriveMin { get; init; }

    public required int Transfers { get; init; }

    public required List<Explanation> Explanations { get; init; }

    public required List<TripWarning> Warnings { get; init; }

    public required string Signature { get; init; }

    /// <summary>Same services in the same order, ignoring which departure: used to avoid near-duplicates.</summary>
    public required string ServicePattern { get; init; }

    /// <summary>Departure minute of every scheduled leg, in order. Two options on the same services are near-duplicates only if all of these are close.</summary>
    public required IReadOnlyList<long> ScheduledDepartures { get; init; }

    public int PreservedLegs { get; init; }

    public bool WithinTolerance { get; set; }

    /// <summary>Elapsed minutes measured from the search anchor (forward) or to it (backward); filled by the ranker.</summary>
    public long ElapsedMin { get; set; }

    /// <summary>Sort key before ranking: earliest arrival (forward) or latest departure (backward).</summary>
    public long ElapsedKey(bool backward) => backward ? -DepartMin : ArriveMin;
}
