using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;

namespace Dhathuru.Domain.Tests;

public class IdTests
{
    [Fact]
    public void New_ids_carry_prefix_and_a_valid_ulid()
    {
        var id = BookingId.New();
        Assert.StartsWith("bkg_", id.Value, StringComparison.Ordinal);
        Assert.True(Ulid.IsValid(id.Value.AsSpan(4)));
        Assert.Equal(30, id.Value.Length);
    }

    [Fact]
    public void Ids_created_in_a_burst_are_unique_and_sorted()
    {
        var ids = Enumerable.Range(0, 5000).Select(_ => Ulid.NewString()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), ids);
    }

    [Fact]
    public void Parse_round_trips_and_rejects_other_prefixes()
    {
        var id = TicketId.New();
        Assert.Equal(id, TicketId.Parse(id.Value));
        Assert.False(TicketId.TryParse(BookingId.New().Value, out _));
        Assert.False(TicketId.TryParse("tkt_nope", out _));
        Assert.False(TicketId.TryParse(null, out _));
    }

    [Theory]
    [InlineData("tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1M2", true)]
    [InlineData("tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1MI", false)] // I is not in Crockford base32
    [InlineData("tkt_91K8Z3M5Q7R9T2V4W6X8Y0K1M2", false)] // timestamp overflow
    public void Ulid_alphabet_is_enforced(string value, bool valid) =>
        Assert.Equal(valid, TicketId.TryParse(value, out _));

    [Theory]
    [InlineData(PlaceType.Island, "isl_")]
    [InlineData(PlaceType.Airport, "apt_")]
    [InlineData(PlaceType.Jetty, "jty_")]
    [InlineData(PlaceType.TaxiStand, "txs_")]
    public void Place_ids_encode_their_type(PlaceType type, string prefix)
    {
        var id = PlaceId.New(type);
        Assert.StartsWith(prefix, id.Value, StringComparison.Ordinal);
        Assert.Equal(type, id.Type);
        Assert.True(PlaceId.TryParse(id.Value, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Place_ids_reject_non_place_prefixes() =>
        Assert.False(PlaceId.TryParse(BookingId.New().Value, out _));

    [Fact]
    public void Contract_example_ids_are_valid()
    {
        Assert.True(PlaceId.TryParse("isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2", out _));
        Assert.True(TripOptionId.TryParse("trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2", out _));
        Assert.True(LegId.TryParse("leg_01K8Z3M5Q7R9T2V4W6X8Y0G1H2", out _));
    }
}
