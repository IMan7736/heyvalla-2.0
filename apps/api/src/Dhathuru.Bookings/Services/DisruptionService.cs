using System.Text.Json.Nodes;
using Dhathuru.Bookings.Domain;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DomainDisruption = Dhathuru.Domain.Trips.Disruption;
using DomainDisruptionKind = Dhathuru.Domain.Trips.DisruptionKind;
using Disruption = Dhathuru.Bookings.Domain.Disruption;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Reacts to provider delay/cancel reports: records the disruption on the affected legs, tells the traveller, and asks the trip
/// planner for alternatives. It never rebooks: the traveller decides what happens next.
/// </summary>
public sealed class DisruptionService(
    BookingsDbContext db,
    ProviderRegistry providers,
    ITripPlanner planner,
    BookingNotifier notifier,
    TimeProvider clock,
    IOptions<BookingsOptions> options,
    ILogger<DisruptionService> logger)
{
    public async Task<int> ReportAsync(string providerId, string serviceRunId, ServiceRunState state, int delayMinutes, CancellationToken ct)
    {
        var legs = await db.BookingLegs
            .Where(l => l.ProviderId == providerId && l.ServiceRunId == serviceRunId && l.Status == BookingLegStatus.Confirmed)
            .ToListAsync(ct);
        var touched = 0;
        foreach (var bookingId in legs.Select(l => l.BookingId).Distinct())
        {
            var booking = await db.Bookings
                .Include(b => b.Legs).Include(b => b.Passengers).Include(b => b.Disruptions).Include(b => b.Tickets)
                .SingleAsync(b => b.Id == bookingId, ct);
            if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.PartiallyCancelled))
            {
                continue;
            }

            foreach (var leg in booking.Legs.Where(l => l.ProviderId == providerId && l.ServiceRunId == serviceRunId && l.Status == BookingLegStatus.Confirmed))
            {
                if (await ApplyAsync(booking, leg, state, delayMinutes, ct))
                {
                    touched++;
                }
            }
        }
        return touched;
    }

    private async Task<bool> ApplyAsync(Booking booking, BookingLeg leg, ServiceRunState state, int delayMinutes, CancellationToken ct)
    {
        var cancelled = state == ServiceRunState.Cancelled;
        var open = booking.Disruptions.FirstOrDefault(d => d.BookingLegId == leg.Id && d.State != DisruptionState.Resolved);
        var disrupted = cancelled || (state == ServiceRunState.Delayed && delayMinutes > 0);
        var now = clock.GetUtcNow();

        if (!disrupted)
        {
            if (open is null)
            {
                return false;
            }
            open.Resolve();
            booking.Note("disruption_resolved", now, "provider", leg.Id);
            await db.SaveChangesAsync(ct);
            return true;
        }

        if (open is not null && open.Cancelled == cancelled && open.DelayMinutes == delayMinutes)
        {
            return false; // same report again
        }

        if (open is null)
        {
            open = new Disruption(Ulid.NewString(), booking.Id, leg.Id, leg.LegIndex, cancelled, delayMinutes, now);
            booking.AddDisruption(open);
            db.Entry(open).State = EntityState.Added;
        }
        else
        {
            open.Update(cancelled, delayMinutes);
        }

        booking.Note(cancelled ? "leg_cancelled_by_provider" : "leg_delayed", now, "provider", $"{leg.Id}:{delayMinutes}");
        await db.SaveChangesAsync(ct);

        open.SetSuggestion(await SuggestAsync(booking, leg, cancelled, delayMinutes, now, ct));
        open.MarkNotified(now);
        await db.SaveChangesAsync(ct);
        await notifier.DisruptionAsync(booking, open, ct);
        return true;
    }

    private async Task<string> SuggestAsync(Booking booking, BookingLeg leg, bool cancelled, int delayMinutes, DateTimeOffset now, CancellationToken ct)
    {
        var options = new JsonArray();
        try
        {
            var legId = LegId.Parse(JsonNode.Parse(leg.LegJson)!["id"]!.GetValue<string>());
            var result = await planner.ReplanAsync(new Dhathuru.Domain.Trips.ReplanQuery(
                BookingId.Parse(booking.Id),
                now,
                new DomainDisruption(legId, cancelled ? DomainDisruptionKind.Cancelled : DomainDisruptionKind.Delayed, cancelled ? null : delayMinutes)), ct);
            foreach (var option in result.Options.Take(3))
            {
                options.Add(new JsonObject
                {
                    ["optionId"] = option.Id.Value,
                    ["departAt"] = option.DepartAt.ToUniversalTime().ToString("O"),
                    ["arriveAt"] = option.ArriveAt.ToUniversalTime().ToString("O"),
                    ["durationMin"] = option.DurationMin,
                    ["transfers"] = option.Transfers,
                    ["total"] = LegSnapshots.MoneyNode(option.Price.Total),
                    ["riskLevel"] = option.Risk.Level.ToString().ToLowerInvariant(),
                    ["labels"] = new JsonArray([.. option.Labels.Select(l => (JsonNode?)JsonValue.Create(l.ToString()))]),
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Replan suggestion unavailable for {BookingId}.", booking.Id);
        }

        return new JsonObject { ["requiresConsent"] = true, ["options"] = options }.ToJsonString();
    }

    public async Task<BookingDto> AcknowledgeAsync(CurrentUser user, string bookingId, string disruptionId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Legs).Include(b => b.Passengers).Include(b => b.Payments).Include(b => b.Disruptions).Include(b => b.Tickets)
            .SingleOrDefaultAsync(b => b.Id == bookingId && b.UserId == user.Id, ct) ?? throw ProblemException.NotFound();
        var disruption = booking.Disruptions.SingleOrDefault(d => d.Id == disruptionId) ?? throw ProblemException.NotFound();
        disruption.Acknowledge();
        await db.SaveChangesAsync(ct);
        return BookingViews.ToDto(booking);
    }

    /// <summary>Polls providers for status of upcoming confirmed legs. Intended for the background monitor.</summary>
    public async Task<int> PollAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var horizon = now + options.Value.DisruptionHorizon;
        var candidates = await db.BookingLegs.AsNoTracking()
            .Where(l => l.Status == BookingLegStatus.Confirmed && l.ServiceRunId != null && l.Bookable)
            .Select(l => new { l.ProviderId, l.ServiceRunId, l.LegJson })
            .ToListAsync(ct);

        var runs = candidates
            .Select(c => (c.ProviderId, Run: c.ServiceRunId!, Depart: LegSnapshots.DepartAt(JsonNode.Parse(c.LegJson)!.AsObject())))
            .Where(c => c.Depart > now - TimeSpan.FromHours(2) && c.Depart <= horizon)
            .Select(c => (c.ProviderId, c.Run))
            .Distinct();

        var total = 0;
        foreach (var (providerId, run) in runs)
        {
            if (providers.Find(providerId) is not { } provider)
            {
                continue;
            }

            try
            {
                var status = await provider.GetStatusAsync(run, ct);
                total += await ReportAsync(providerId, run, status.State, status.DelayMinutes, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Status poll failed for {Provider}/{Run}.", providerId, run);
            }
        }
        return total;
    }
}
