using Dhathuru.Domain.Trips;

namespace Dhathuru.TripEngine.Configuration;

/// <summary>
/// Every threshold the engine uses. Nothing is hard-coded elsewhere; each default carries its rationale
/// (also summarised in docs/engine/ALGORITHM.md). Values are minutes unless the name says otherwise.
/// </summary>
public sealed record EngineOptions
{
    /// <summary>Most options returned. Six fills the result list without overwhelming the traveller.</summary>
    public int MaxOptions { get; init; } = 6;

    /// <summary>Longest itinerary (legs, flexible legs included). Seven covers ferry-taxi-flight-taxi plus two connectors.</summary>
    public int MaxLegs { get; init; } = 7;

    /// <summary>How far past the search time the router looks. 36 h reaches "the first thing tomorrow morning".</summary>
    public int HorizonMin { get; init; } = 36 * 60;

    /// <summary>Longest wait accepted between two scheduled legs. 18 h allows an overnight stay, never an absurd one.</summary>
    public int MaxConnectionWaitMin { get; init; } = 18 * 60;

    /// <summary>A wait at least this long that spans local night is flagged OVERNIGHT_REQUIRED.</summary>
    public int OvernightWaitMin { get; init; } = 6 * 60;

    /// <summary>Later options considered once a first arrival is known. 12 h keeps the safe-but-later alternative in play.</summary>
    public int AlternativeWindowMin { get; init; } = 12 * 60;

    /// <summary>Per (label, service, destination) the router tries the first N feasible departures: tight, comfortable, safe.</summary>
    public int RunsPerService { get; init; } = 3;

    /// <summary>Cap on non-dominated labels per (place, last mode). Bounds work; the Pareto front is far smaller in practice.</summary>
    public int MaxLabelsPerKey { get; init; } = 12;

    /// <summary>Whole local days of timetable fetched before / after the search date.</summary>
    /// <summary>Wall-clock cap for the extra "next best without this run" searches that supply backup options.</summary>
    public int AlternativeSearchBudgetMs { get; init; } = 120;

    public int SnapshotDaysBefore { get; init; } = 1;

    public int SnapshotDaysAfter { get; init; } = 1;

    public ConnectionOptions Connections { get; init; } = new();

    public TaxiOptions Taxi { get; init; } = new();

    public DelayPriorOptions Delays { get; init; } = new();

    public PricingOptions Pricing { get; init; } = new();

    public ScoringOptions Scoring { get; init; } = new();

    public ToleranceProfile Profile(RiskTolerance tolerance) => tolerance switch
    {
        RiskTolerance.Cautious => Scoring.Cautious,
        RiskTolerance.Relaxed => Scoring.Relaxed,
        _ => Scoring.Balanced,
    };
}

/// <summary>Hard minimum buffers. These are floors: an itinerary below them is never returned as a normal option.</summary>
public sealed record ConnectionOptions
{
    /// <summary>Used when a flight run publishes no check-in cutoff (0). Domestic desks typically close 45 min before.</summary>
    public int FlightCheckInDefaultMin { get; init; } = 45;

    /// <summary>Seaplane lounge check-in when the run publishes none.</summary>
    public int SeaplaneCheckInDefaultMin { get; init; } = 30;

    /// <summary>Ferries, speedboats and resort transfers stop boarding before departure.</summary>
    public int BoatBoardingClosureMin { get; init; } = 15;

    public int BusBoardingMin { get; init; } = 5;

    /// <summary>Time to disembark and walk off after each kind of leg.</summary>
    public int AlightFlightMin { get; init; } = 20;

    public int AlightSeaplaneMin { get; init; } = 15;

    public int AlightBoatMin { get; init; } = 10;

    public int AlightOtherMin { get; init; } = 3;

    /// <summary>Extra minutes per passenger beyond two (luggage, kids). Applies to every connection.</summary>
    public int PerExtraPassengerMin { get; init; } = 2;

    /// <summary>Data-driven hub overrides: extra required minutes at a place id (for example a congested terminal).</summary>
    public IReadOnlyDictionary<string, int> ExtraMinutesByPlace { get; init; } = new Dictionary<string, int>();

    /// <summary>Walk assumed between a place and its parent (jetty and its island) when the snapshot has no link. Flagged as assumed.</summary>
    public int ImplicitLinkMin { get; init; } = 5;
}

public sealed record TaxiOptions
{
    /// <summary>A taxi is ordered when the previous leg arrives; this is the pick-up lead.</summary>
    public int PickupLeadMin { get; init; } = 8;

    /// <summary>Local service window (minutes of day). Outside it no taxi is offered. Conservative: small islands have no night taxis.</summary>
    public int ServiceStartMinuteOfDay { get; init; } = 5 * 60;

    public int ServiceEndMinuteOfDay { get; init; } = 23 * 60 + 30;

    /// <summary>One vehicle carries this many passengers; fares are per vehicle.</summary>
    public int VehicleCapacity { get; init; } = 4;
}

/// <summary>Priors used when no (or too little) delay history exists. Late probability and mean lateness in minutes.</summary>
public sealed record DelayPriorOptions
{
    public DelayPrior Ferry { get; init; } = new(0.25, 12);

    public DelayPrior Speedboat { get; init; } = new(0.20, 10);

    public DelayPrior Seaplane { get; init; } = new(0.25, 15);

    public DelayPrior DomesticFlight { get; init; } = new(0.30, 20);

    public DelayPrior Bus { get; init; } = new(0.20, 8);

    public DelayPrior ResortTransfer { get; init; } = new(0.20, 10);

    /// <summary>Taxi trip-time noise. Mean lateness is the larger of this and <see cref="TaxiNoiseShareOfDuration"/> of the ride.</summary>
    public DelayPrior Taxi { get; init; } = new(0.15, 4);

    public double TaxiNoiseShareOfDuration { get; init; } = 0.30;

    /// <summary>Samples at which observed history gets half the weight against the prior.</summary>
    public int ShrinkageSamples { get; init; } = 20;

    /// <summary>History with fewer samples than this is ignored for explanations (still blended for risk).</summary>
    public int MinSamplesForExplanation { get; init; } = 30;

    public double GoodOnTimeRate { get; init; } = 0.90;

    public double PoorOnTimeRate { get; init; } = 0.70;
}

public readonly record struct DelayPrior(double LateProbability, double MeanLateMin);

public sealed record PricingOptions
{
    /// <summary>Share of the adult fare paid for a child (percent). Full fare unless a provider says otherwise.</summary>
    public int ChildFarePercent { get; init; } = 100;

    /// <summary>Share of the adult fare paid for an infant (percent). Infants in arms travel free.</summary>
    public int InfantFarePercent { get; init; } = 0;
}

public sealed record ScoringOptions
{
    public ToleranceProfile Cautious { get; init; } = new()
    {
        MaxMissProbability = 0.03,
        MinSlackMin = 10,
        ComfortMarginMin = 30,
        WeightTime = 0.25,
        WeightPrice = 0.15,
        WeightRisk = 0.50,
        WeightTransfers = 0.10,
    };

    public ToleranceProfile Balanced { get; init; } = new()
    {
        MaxMissProbability = 0.08,
        MinSlackMin = 5,
        ComfortMarginMin = 20,
        WeightTime = 0.35,
        WeightPrice = 0.25,
        WeightRisk = 0.30,
        WeightTransfers = 0.10,
    };

    public ToleranceProfile Relaxed { get; init; } = new()
    {
        MaxMissProbability = 0.15,
        MinSlackMin = 0,
        ComfortMarginMin = 10,
        WeightTime = 0.40,
        WeightPrice = 0.30,
        WeightRisk = 0.20,
        WeightTransfers = 0.10,
    };

    /// <summary>Miss probability at which the risk term of the score saturates (penalty 1).</summary>
    public double RiskSaturation { get; init; } = 0.20;

    /// <summary>A trip as long as (fastest + this fraction of the fastest, at least <see cref="TimeFloorMin"/>) saturates the time penalty.</summary>
    public double TimeSpreadFraction { get; init; } = 1.0;

    public int TimeFloorMin { get; init; } = 120;

    /// <summary>Transfers at which the transfer term saturates.</summary>
    public int TransferSaturation { get; init; } = 5;

    /// <summary>Score bonus per confirmed leg a re-plan keeps (points on the 0..100 scale).</summary>
    public double ReplanKeptLegBonus { get; init; } = 4;

    /// <summary>Risk levels: below Low is "low", below High is "medium", else "high".</summary>
    public double LowRiskBelow { get; init; } = 0.05;

    public double HighRiskFrom { get; init; } = 0.12;

    /// <summary>Two options closer than this on departure with the same services are near-duplicates.</summary>
    public int DiversityDepartureMin { get; init; } = 30;
}

/// <summary>How much risk a traveller accepts and how the score weighs the four criteria for them.</summary>
public sealed record ToleranceProfile
{
    /// <summary>Options with a higher whole-trip miss probability are only returned if nothing safer exists (and are warned).</summary>
    public double MaxMissProbability { get; init; }

    /// <summary>Slack beyond the hard floor every connection must have to count as inside the tolerance.</summary>
    public int MinSlackMin { get; init; }

    /// <summary>Comfortable extra buffer reported as <c>comfortMinutes</c> and used for BUFFER_COMFORTABLE.</summary>
    public int ComfortMarginMin { get; init; }

    public double WeightTime { get; init; }

    public double WeightPrice { get; init; }

    public double WeightRisk { get; init; }

    public double WeightTransfers { get; init; }
}
