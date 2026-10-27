using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Policies;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Transport;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Tests;

public class StateMachineTests
{
    private static readonly HashSet<(BookingStatus, BookingStatus)> Legal =
    [
        (BookingStatus.Draft, BookingStatus.Held), (BookingStatus.Draft, BookingStatus.Failed),
        (BookingStatus.Held, BookingStatus.PaymentPending), (BookingStatus.Held, BookingStatus.Expired),
        (BookingStatus.Held, BookingStatus.Cancelled), (BookingStatus.Held, BookingStatus.Failed),
        (BookingStatus.PaymentPending, BookingStatus.Confirmed), (BookingStatus.PaymentPending, BookingStatus.Failed),
        (BookingStatus.PaymentPending, BookingStatus.Expired), (BookingStatus.PaymentPending, BookingStatus.Held),
        (BookingStatus.Confirmed, BookingStatus.PartiallyCancelled), (BookingStatus.Confirmed, BookingStatus.Cancelled),
        (BookingStatus.PartiallyCancelled, BookingStatus.PartiallyCancelled), (BookingStatus.PartiallyCancelled, BookingStatus.Cancelled),
        (BookingStatus.Cancelled, BookingStatus.Refunded),
    ];

    [Fact]
    public void Only_documented_transitions_are_allowed()
    {
        foreach (var from in Enum.GetValues<BookingStatus>())
        {
            foreach (var to in Enum.GetValues<BookingStatus>())
            {
                Assert.Equal(Legal.Contains((from, to)), BookingStateMachine.CanTransition(from, to));
            }
        }
    }

    [Theory]
    [InlineData(BookingStatus.Failed)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Refunded)]
    public void Terminal_states_go_nowhere(BookingStatus status) => Assert.True(BookingStateMachine.IsTerminal(status));

    [Fact]
    public void Illegal_transition_throws_and_leaves_status_unchanged()
    {
        var booking = NewBooking();
        var ex = Assert.Throws<InvalidBookingTransitionException>(() => booking.Transition(BookingStatus.Confirmed, DateTimeOffset.UnixEpoch, "t"));
        Assert.Equal(BookingStatus.Draft, ex.From);
        Assert.Equal(BookingStatus.Draft, booking.Status);
    }

    [Fact]
    public void Every_transition_is_audited_and_hold_expiry_clears_when_seats_are_released()
    {
        var now = DateTimeOffset.UnixEpoch;
        var booking = NewBooking();
        booking.StartHold(now.AddMinutes(12), now, "t");
        Assert.Equal(now.AddMinutes(12), booking.HoldExpiresAt);
        booking.Transition(BookingStatus.Expired, now.AddMinutes(13), "system");
        Assert.Null(booking.HoldExpiresAt);
        Assert.Equal(["created", "held", "transition"], booking.Audit.Select(a => a.Action));
    }

    [Fact]
    public void Same_state_transition_is_a_no_op_so_retries_are_idempotent()
    {
        var booking = NewBooking();
        booking.Transition(BookingStatus.Draft, DateTimeOffset.UnixEpoch, "t");
        Assert.Single(booking.Audit);
    }

    private static Booking NewBooking() =>
        Booking.Create("bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2", "usr_x", "trp_x", new(100), "[]", null, "en", "k", "h", DateTimeOffset.UnixEpoch);
}

public class RefundCalculatorTests
{
    private readonly CancellationPolicyCatalog _catalog = new();

    [Theory]
    [InlineData(48, 9000, 9000)]
    [InlineData(24, 9000, 9000)]
    [InlineData(10, 9000, 4500)]
    [InlineData(2, 9000, 4500)]
    [InlineData(1, 9000, 0)]
    public void Ferry_ladder(int hoursBefore, long paid, long expected)
    {
        var now = DateTimeOffset.UnixEpoch;
        var quote = RefundCalculator.Quote(0, _catalog.For("any", TransportMode.Ferry), paid, now.AddHours(hoursBefore), now);
        Assert.True(quote.Cancellable);
        Assert.Equal(expected, quote.Refund.AmountMinor);
    }

    [Fact]
    public void Flight_keeps_its_non_refundable_fee_and_never_refunds_more_than_paid()
    {
        var now = DateTimeOffset.UnixEpoch;
        var policy = _catalog.For("any", TransportMode.DomesticFlight);
        Assert.Equal(80000, RefundCalculator.Quote(0, policy, 85000, now.AddHours(100), now).Refund.AmountMinor);
        Assert.Equal(0, RefundCalculator.Quote(0, policy, 3000, now.AddHours(100), now).Refund.AmountMinor);
    }

    [Fact]
    public void A_departed_leg_cannot_be_cancelled()
    {
        var now = DateTimeOffset.UnixEpoch;
        var quote = RefundCalculator.Quote(1, _catalog.For("x", TransportMode.Taxi), 5000, now.AddMinutes(-1), now);
        Assert.False(quote.Cancellable);
        Assert.Equal("LEG_DEPARTED", quote.ReasonCode);
    }

    [Fact]
    public void Unknown_modes_fall_back_to_default_policy()
    {
        Assert.Equal("STANDARD_1H", _catalog.For("x", TransportMode.Walk).Code);
    }
}

public class TicketSignerTests
{
    private static string Key(int seed) => Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + seed)).ToArray());

    private static TicketSigner Signer(string active, params (string Id, int Seed)[] keys) =>
        new(Options.Create(new TicketSigningOptions { ActiveKeyId = active, Keys = keys.ToDictionary(k => k.Id, k => Key(k.Seed)) }));

    [Fact]
    public void Round_trip()
    {
        var signer = Signer("k1", ("k1", 1));
        var payload = signer.Sign("tkt_abc", "nonce1");
        Assert.Equal(TicketVerdict.Valid, signer.Verify(payload, out var parsed));
        Assert.Equal(("tkt_abc", "k1", "nonce1"), (parsed!.TicketId, parsed.KeyId, parsed.Nonce));
    }

    [Theory]
    [InlineData("tkt_abd", "nonce1")]
    [InlineData("tkt_abc", "nonce2")]
    public void Tampering_with_any_field_is_detected(string id, string nonce)
    {
        var signer = Signer("k1", ("k1", 1));
        var parts = signer.Sign("tkt_abc", "nonce1").Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{id}.{nonce}.{parts[4]}";
        Assert.Equal(TicketVerdict.BadSignature, signer.Verify(forged, out _));
    }

    [Fact]
    public void A_different_secret_cannot_forge()
    {
        var attacker = Signer("k1", ("k1", 77));
        var victim = Signer("k1", ("k1", 1));
        Assert.Equal(TicketVerdict.BadSignature, victim.Verify(attacker.Sign("tkt_abc", "n"), out _));
    }

    [Fact]
    public void Rotation_keeps_old_tickets_valid_and_new_tickets_use_the_new_key()
    {
        var before = Signer("k1", ("k1", 1));
        var oldPayload = before.Sign("tkt_abc", "n");
        var after = Signer("k2", ("k1", 1), ("k2", 2));
        Assert.Equal(TicketVerdict.Valid, after.Verify(oldPayload, out _));
        Assert.Equal("k2", after.ActiveKeyId);
        Assert.Contains(".k2.", after.Sign("tkt_x", "n").Replace("v1.", "."), StringComparison.Ordinal);
        Assert.Equal(TicketVerdict.UnknownKey, Signer("k3", ("k3", 3)).Verify(oldPayload, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("dhathuru:v1.")]
    [InlineData("dhathuru:v1.k1.a.b")]
    [InlineData("dhathuru:v1.k1.a.b.c.d")]
    public void Garbage_is_malformed(string payload) =>
        Assert.Equal(TicketVerdict.Malformed, Signer("k1", ("k1", 1)).Verify(payload, out _));

    [Fact]
    public void Short_keys_are_rejected_at_startup()
    {
        var options = Options.Create(new TicketSigningOptions { ActiveKeyId = "k1", Keys = { ["k1"] = Convert.ToBase64String(new byte[8]) } });
        Assert.Throws<InvalidOperationException>(() => new TicketSigner(options));
    }
}

public class QrEncoderTests
{
    [Theory]
    [InlineData(5, 21)]
    [InlineData(14, 21)]
    [InlineData(15, 25)]
    [InlineData(26, 25)]
    [InlineData(27, 29)]
    [InlineData(106, 41)]
    [InlineData(213, 57)]
    public void Picks_the_smallest_version_that_fits(int bytes, int expectedSize)
    {
        var matrix = QrEncoder.Encode(new string('a', bytes));
        Assert.Equal(expectedSize, matrix.GetLength(0));
    }

    [Fact]
    public void Has_finder_patterns_and_timing_lines()
    {
        var m = QrEncoder.Encode("dhathuru");
        var n = m.GetLength(0);
        foreach (var (ox, oy) in new[] { (0, 0), (n - 7, 0), (0, n - 7) })
        {
            for (var y = 0; y < 7; y++)
            {
                for (var x = 0; x < 7; x++)
                {
                    var ring = Math.Max(Math.Abs(x - 3), Math.Abs(y - 3));
                    Assert.Equal(ring != 2, m[oy + y, ox + x]);
                }
            }
        }
        for (var i = 8; i < n - 8; i++)
        {
            Assert.Equal(i % 2 == 0, m[6, i]);
            Assert.Equal(i % 2 == 0, m[i, 6]);
        }
    }

    [Fact]
    public void Dumps_pbm_for_an_external_decoder_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("QR_DUMP_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var texts = new[] { "A", "dhathuru:v1.k2.tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1M2.AbCdEfGhIjKl.3q2-7w_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789ab", new string('z', 213), "Hulhumal\u00e9 \u2192 Maafushi" };
        for (var i = 0; i < texts.Length; i++)
        {
            var m = QrEncoder.Encode(texts[i]);
            var n = m.GetLength(0) + 8;
            var sb = new System.Text.StringBuilder($"P1\n{n * 8} {n * 8}\n");
            for (var y = 0; y < n * 8; y++)
            {
                for (var x = 0; x < n * 8; x++)
                {
                    var mx = x / 8 - 4;
                    var my = y / 8 - 4;
                    var dark = mx >= 0 && my >= 0 && mx < n - 8 && my < n - 8 && m[my, mx];
                    sb.Append(dark ? '1' : '0').Append(' ');
                }
                sb.Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, $"qr{i}.pbm"), sb.ToString());
            File.WriteAllText(Path.Combine(dir, $"qr{i}.txt"), texts[i]);
        }
    }

    [Fact]
    public void Is_deterministic_and_too_long_text_is_rejected()
    {
        Assert.Equal(QrEncoder.ToSvg(QrEncoder.Encode("x")), QrEncoder.ToSvg(QrEncoder.Encode("x")));
        Assert.Throws<ArgumentException>(() => QrEncoder.Encode(new string('a', QrEncoder.MaxBytes + 1)));
    }

    [Fact]
    public void Svg_is_well_formed()
    {
        var svg = QrEncoder.ToSvg(QrEncoder.Encode("dhathuru:v1.test"));
        var doc = System.Xml.Linq.XDocument.Parse(svg);
        Assert.Equal("svg", doc.Root!.Name.LocalName);
    }
}
