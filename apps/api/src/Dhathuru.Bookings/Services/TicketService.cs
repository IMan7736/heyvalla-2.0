using System.Security.Cryptography;
using System.Text;
using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Services;

public sealed record VerifyResult(bool Valid, string? ReasonCode, TicketDto? Ticket);

public sealed class VerificationOptions
{
    public const string Section = "Bookings:Verification";

    /// <summary>Shared secret operators send as <c>X-Operator-Key</c> to consume tickets. Empty disables consumption.</summary>
    public string OperatorKey { get; set; } = "";

    /// <summary>A ticket stops verifying this long after its leg arrives.</summary>
    public TimeSpan ValidAfterArrival { get; set; } = TimeSpan.FromHours(12);
}

public sealed class TicketService(BookingsDbContext db, TicketSigner signer, IOptions<VerificationOptions> options, TimeProvider clock)
{
    public async Task<string> QrSvgAsync(CurrentUser user, string ticketId, CancellationToken ct)
    {
        if (!TicketId.TryParse(ticketId, out _))
        {
            throw ProblemException.NotFound();
        }

        var ticket = await db.Tickets.AsNoTracking()
            .Join(db.Bookings.AsNoTracking(), t => t.BookingId, b => b.Id, (t, b) => new { Ticket = t, b.UserId })
            .Where(x => x.Ticket.Id == ticketId && x.UserId == user.Id)
            .Select(x => x.Ticket)
            .SingleOrDefaultAsync(ct) ?? throw ProblemException.NotFound();

        if (ticket.Status != TicketStatus.Valid)
        {
            throw ProblemException.Conflict("TICKET_NOT_VALID", new Dictionary<string, object?> { ["status"] = WireNames.Of(ticket.Status) });
        }

        return QrEncoder.ToSvg(QrEncoder.Encode(signer.Sign(ticket.Id, ticket.Nonce, ticket.KeyId)));
    }

    public async Task<VerifyResult> VerifyAsync(string payload, bool consume, string? operatorKey, CancellationToken ct)
    {
        if (consume && !IsOperator(operatorKey))
        {
            throw ProblemException.Forbidden("OPERATOR_KEY_REQUIRED");
        }

        var verdict = signer.Verify(payload, out var parsed);
        if (verdict != TicketVerdict.Valid || parsed is null)
        {
            return new VerifyResult(false, verdict switch
            {
                TicketVerdict.UnknownKey => "TICKET_UNKNOWN_KEY",
                TicketVerdict.BadSignature => "TICKET_SIGNATURE_INVALID",
                _ => "TICKET_MALFORMED",
            }, null);
        }

        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == parsed.TicketId, ct);
        // The nonce is bound to the stored ticket, so a validly signed payload for a reissued ticket does not replay.
        if (ticket is null || !FixedEquals(ticket.Nonce, parsed.Nonce))
        {
            return new VerifyResult(false, "TICKET_NOT_FOUND", null);
        }

        var leg = await db.BookingLegs.AsNoTracking().SingleAsync(l => l.Id == ticket.BookingLegId, ct);
        var now = clock.GetUtcNow();
        var arrive = LegSnapshots.ArriveAt(System.Text.Json.Nodes.JsonNode.Parse(leg.LegJson)!.AsObject());
        if (ticket.Status == TicketStatus.Valid && arrive + options.Value.ValidAfterArrival < now)
        {
            return new VerifyResult(false, "TICKET_EXPIRED", BookingViews.ToDto(ticket, leg));
        }

        switch (ticket.Status)
        {
            case TicketStatus.Cancelled:
                return new VerifyResult(false, "TICKET_CANCELLED", BookingViews.ToDto(ticket, leg));
            case TicketStatus.Expired:
                return new VerifyResult(false, "TICKET_EXPIRED", BookingViews.ToDto(ticket, leg));
            case TicketStatus.Used:
                return new VerifyResult(false, "TICKET_ALREADY_USED", BookingViews.ToDto(ticket, leg));
        }

        if (!consume)
        {
            return new VerifyResult(true, null, BookingViews.ToDto(ticket, leg));
        }

        // Atomic: of two scanners racing on the same ticket, exactly one flips valid -> used.
        var changed = await db.Tickets
            .Where(t => t.Id == ticket.Id && t.Status == TicketStatus.Valid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Used).SetProperty(t => t.UsedAt, now), ct);
        await db.Entry(ticket).ReloadAsync(ct);
        return changed == 1
            ? new VerifyResult(true, null, BookingViews.ToDto(ticket, leg))
            : new VerifyResult(false, "TICKET_ALREADY_USED", BookingViews.ToDto(ticket, leg));
    }

    private bool IsOperator(string? supplied)
    {
        var configured = options.Value.OperatorKey;
        return !string.IsNullOrEmpty(configured) && supplied is not null && FixedEquals(configured, supplied);
    }

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
