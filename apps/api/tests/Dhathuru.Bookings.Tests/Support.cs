using System.Collections.Concurrent;
using Dhathuru.Bookings;
using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Dhathuru.Bookings.Tests;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public DateTimeOffset Now => _now;
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithDatabase("dhathuru_thila_test").WithUsername("dhathuru").WithPassword("test-only-password").Build();

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(5432);

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class FakeProvider(string providerId = "fake-ferry", TimeProvider? clock = null) : ITransportProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _seatsLeft = [];
    private readonly Dictionary<string, (string Run, int Seats)> _holds = [];
    private readonly Dictionary<string, string> _holdByKey = [];
    private readonly HashSet<string> _confirmed = [];
    private int _next;

    public ConcurrentBag<string> Calls { get; } = [];

    public string ProviderId { get; } = providerId;

    public ProviderKind Kind => ProviderKind.Mock;

    public Func<string, bool> FailHold { get; set; } = _ => false;

    public Func<string, bool> FailConfirm { get; set; } = _ => false;

    public Func<string, bool> FailCancel { get; set; } = _ => false;

    public ServiceStatus Status { get; set; } = new("run", ServiceRunState.OnTime, 0, DateTimeOffset.UnixEpoch);

    public void SetSeats(string run, int seats)
    {
        lock (_gate)
        {
            _seatsLeft[run] = seats;
        }
    }

    public int SeatsLeft(string run)
    {
        lock (_gate)
        {
            return _seatsLeft[run];
        }
    }

    public int ActiveHolds
    {
        get
        {
            lock (_gate)
            {
                return _holds.Count;
            }
        }
    }

    public int ConfirmedCount
    {
        get
        {
            lock (_gate)
            {
                return _confirmed.Count;
            }
        }
    }

    public Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<ServiceDeparture>>([]);

    public Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) => Task.FromResult(new Availability(true));

    public Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct)
    {
        Calls.Add($"hold:{r.IdempotencyKey}");
        if (FailHold(r.ServiceRunId))
        {
            return Task.FromResult(new HoldResult(false, FailureCode: "SOLD_OUT"));
        }

        lock (_gate)
        {
            if (_holdByKey.TryGetValue(r.IdempotencyKey, out var existing))
            {
                return Task.FromResult(new HoldResult(true, existing, (clock ?? TimeProvider.System).GetUtcNow().Add(r.HoldFor)));
            }

            var left = _seatsLeft.GetValueOrDefault(r.ServiceRunId, 100);
            if (left < r.Passengers)
            {
                return Task.FromResult(new HoldResult(false, FailureCode: "SOLD_OUT"));
            }

            _seatsLeft[r.ServiceRunId] = left - r.Passengers;
            var reference = $"H{++_next}";
            _holds[reference] = (r.ServiceRunId, r.Passengers);
            _holdByKey[r.IdempotencyKey] = reference;
            return Task.FromResult(new HoldResult(true, reference, (clock ?? TimeProvider.System).GetUtcNow().Add(r.HoldFor)));
        }
    }

    public Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct)
    {
        Calls.Add($"confirm:{r.IdempotencyKey}");
        if (FailConfirm(r.HoldReference))
        {
            return Task.FromResult(new ConfirmResult(false, FailureCode: "PROVIDER_REJECTED"));
        }

        lock (_gate)
        {
            if (!_holds.ContainsKey(r.HoldReference) && !_confirmed.Contains("P-" + r.HoldReference))
            {
                return Task.FromResult(new ConfirmResult(false, FailureCode: "HOLD_EXPIRED"));
            }

            _confirmed.Add("P-" + r.HoldReference);
            return Task.FromResult(new ConfirmResult(true, "P-" + r.HoldReference));
        }
    }

    public Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct)
    {
        Calls.Add($"cancel:{r.IdempotencyKey}");
        if (FailCancel(r.ProviderReference))
        {
            return Task.FromResult(new CancelResult(false, FailureCode: "PROVIDER_DOWN"));
        }

        lock (_gate)
        {
            var holdRef = r.ProviderReference.StartsWith("P-", StringComparison.Ordinal) ? r.ProviderReference[2..] : r.ProviderReference;
            if (_holds.Remove(holdRef, out var hold))
            {
                _seatsLeft[hold.Run] = _seatsLeft.GetValueOrDefault(hold.Run) + hold.Seats;
                _confirmed.Remove("P-" + holdRef);
            }
            return Task.FromResult(new CancelResult(true));
        }
    }

    public Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct) => Task.FromResult(Status with { ServiceRunId = serviceRunId });
}

/// <summary>Wraps the fake gateway so a test can make refunds fail and then recover.</summary>
public sealed class FlakyGateway(FakeGateway inner) : Payments.IPaymentGateway
{
    public bool FailRefunds { get; set; }

    public FakeGateway Inner { get; } = inner;

    public string GatewayId => Inner.GatewayId;

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct) => Inner.ChargeAsync(request, ct);

    public Task<ChargeResult> CompleteChallengeAsync(CompleteChallengeRequest request, CancellationToken ct) => Inner.CompleteChallengeAsync(request, ct);

    public Task<ChargeResult?> LookupAsync(string idempotencyKey, CancellationToken ct) => Inner.LookupAsync(idempotencyKey, ct);

    public Task<RefundOutcome> RefundAsync(Payments.RefundRequest request, CancellationToken ct) =>
        FailRefunds ? throw new HttpRequestException("gateway unreachable") : Inner.RefundAsync(request, ct);
}

public sealed class StubPlanner : ITripPlanner
{
    public Dictionary<string, TripOption> Options { get; } = [];

    public int ReplanCalls { get; private set; }

    public Task<TripSearchResult> SearchAsync(TripSearchQuery query, CancellationToken ct) => throw new NotSupportedException();

    public Task<TripOption?> GetOptionAsync(TripOptionId id, CancellationToken ct) => Task.FromResult(Options.GetValueOrDefault(id.Value));

    public Task<TripSearchResult> ReplanAsync(ReplanQuery query, CancellationToken ct)
    {
        ReplanCalls++;
        return Task.FromResult(new TripSearchResult(SearchId.New(), Options.Values.Take(1).ToList()));
    }
}

public sealed class RecordingEmail : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = [];

    public bool Throw { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Throw)
        {
            throw new InvalidOperationException("smtp down");
        }
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public sealed class Harness : IAsyncDisposable
{
    private static int _databases;

    public required ServiceProvider Root { get; init; }

    public required TestClock Clock { get; init; }

    public required StubPlanner Planner { get; init; }

    public required FakeProvider Ferry { get; init; }

    public required FakeProvider Flight { get; init; }

    public required RecordingEmail Email { get; init; }

    public required FakeGateway Gateway { get; init; }

    public required FlakyGateway Flaky { get; init; }

    public required IConfiguration Configuration { get; init; }

    public static async Task<Harness> CreateAsync(PostgresFixture pg, Action<Dictionary<string, string?>>? configure = null, bool simulation = false)
    {
        var database = $"thila_{Interlocked.Increment(ref _databases)}_{Guid.NewGuid():N}"[..40];
        var settings = new Dictionary<string, string?>
        {
            ["Database:Host"] = pg.Host,
            ["Database:Port"] = pg.Port.ToString(),
            ["Database:Name"] = database,
            ["Database:Username"] = "dhathuru",
            ["Database:Password"] = "test-only-password",
            ["Bookings:Tickets:ActiveKeyId"] = "k2",
            ["Bookings:Tickets:Keys:k1"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()),
            ["Bookings:Tickets:Keys:k2"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 101)).ToArray()),
            ["Bookings:Verification:OperatorKey"] = "operator-test-key",
            ["Bookings:PaymentTimeout"] = "00:00:00.300",
            ["Bookings:EnableSimulation"] = simulation ? "true" : "false",
            ["Bookings:FakeGateway:SlowDelay"] = "00:00:00.100",
        };
        configure?.Invoke(settings);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var clock = new TestClock();
        var planner = new StubPlanner();
        var ferry = new FakeProvider("fake-ferry", clock);
        var flight = new FakeProvider("fake-flight", clock);
        var email = new RecordingEmail();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<ITripPlanner>(planner);
        services.AddSingleton<ITransportProvider>(ferry);
        services.AddSingleton<ITransportProvider>(flight);
        services.AddSingleton<IEmailSender>(email);
        var fake = new FakeGateway(Microsoft.Extensions.Options.Options.Create(new FakeGatewayOptions { SlowDelay = TimeSpan.FromMilliseconds(100) }));
        var flaky = new FlakyGateway(fake);
        services.AddSingleton<Payments.IPaymentGateway>(flaky);
        services.AddBookingsModule(configuration);
        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Database.MigrateAsync();
        }

        return new Harness
        {
            Root = provider,
            Clock = clock,
            Planner = planner,
            Ferry = ferry,
            Flight = flight,
            Email = email,
            Gateway = fake,
            Flaky = flaky,
            Configuration = configuration,
        };
    }

    public AsyncServiceScope Scope() => Root.CreateAsyncScope();

    public static CurrentUser User(string? id = null) => new(id ?? UserId.New().Value, "traveller@example.test", "en");

    /// <summary>A two-leg trip: ferry then domestic flight, 1 hour apart, departing 24 hours after the test clock.</summary>
    public TripOption AddTwoLegOption(long ferryMinor = 9000, long flightMinor = 85000, string ferryRun = "ferry-run-1", string flightRun = "flight-run-1")
    {
        var depart = Clock.Now.AddHours(24);
        var option = Option(
            Leg(TransportMode.Ferry, "fake-ferry", ferryRun, depart, depart.AddMinutes(90), ferryMinor),
            Leg(TransportMode.DomesticFlight, "fake-flight", flightRun, depart.AddMinutes(150), depart.AddMinutes(210), flightMinor));
        Planner.Options[option.Id.Value] = option;
        return option;
    }

    public TripOption Option(params Leg[] legs)
    {
        var total = Money.Sum(legs.Select(l => l.Price));
        return new TripOption(
            TripOptionId.New(), legs[0].DepartAt, legs[^1].ArriveAt, legs.Length - 1, legs, new TripPrice(total, []), [],
            new Risk(0.02, RiskLevel.Low), [TripLabel.BestOverall], 1, [], []);
    }

    public static Leg Leg(TransportMode mode, string providerId, string run, DateTimeOffset depart, DateTimeOffset arrive, long priceMinor, bool bookable = true) =>
        new(LegId.New(), mode, providerId,
            Place(PlaceType.Island, "Maafushi"), Place(PlaceType.Jetty, "Hulhumalé Ferry Terminal"),
            depart, arrive, new Money(priceMinor), bookable, PricingKind.Fixed, [], ServiceId.New(), bookable ? run : null);

    public static PlaceRef Place(PlaceType type, string name) =>
        new(PlaceId.New(type), type, new Dictionary<string, string> { ["en"] = name }, new GeoPoint(3.94, 73.49));

    public static CreateBookingCommand Command(TripOption option, int adults = 1, int children = 0, int infants = 0)
    {
        var passengers = new List<PassengerInput>();
        for (var i = 0; i < adults; i++) passengers.Add(new PassengerInput(null, $"Adult {i + 1}", "adult", null));
        for (var i = 0; i < children; i++) passengers.Add(new PassengerInput(null, $"Child {i + 1}", "child", null));
        for (var i = 0; i < infants; i++) passengers.Add(new PassengerInput(null, $"Infant {i + 1}", "infant", null));
        return new CreateBookingCommand(option.Id.Value, passengers);
    }

    public static string Key() => Guid.NewGuid().ToString("N");

    public async ValueTask DisposeAsync()
    {
        await using (var scope = Root.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Database.EnsureDeletedAsync();
        }
        await Root.DisposeAsync();
    }
}
