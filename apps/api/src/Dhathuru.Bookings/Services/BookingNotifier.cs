using Dhathuru.Bookings.Domain;
using Dhathuru.Domain.Seams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Sends booking emails through the shared <see cref="IEmailSender"/>. Delivery is best effort: a mail failure is logged
/// and never changes a booking outcome. Template codes: booking.confirmed, booking.failed, booking.cancelled, booking.disruption.
/// </summary>
public sealed class BookingNotifier(IEmailSender sender, Tickets.TicketSigner signer, IOptions<BookingsOptions> options, ILogger<BookingNotifier> logger)
{
    public Task ConfirmedAsync(Booking booking, CancellationToken ct) =>
        SendAsync(booking, "booking.confirmed", [], withItinerary: true, ct);

    public Task FailedAsync(Booking booking, CancellationToken ct) =>
        SendAsync(booking, "booking.failed", new() { ["failureCode"] = booking.FailureCode ?? "" }, false, ct);

    public Task CancelledAsync(Booking booking, long refundMinor, CancellationToken ct) =>
        SendAsync(booking, "booking.cancelled", new() { ["refund"] = Format(refundMinor, booking.Currency) }, false, ct);

    public Task DisruptionAsync(Booking booking, Disruption disruption, CancellationToken ct) =>
        SendAsync(booking, "booking.disruption", new()
        {
            ["kind"] = disruption.Cancelled ? "cancelled" : "delayed",
            ["delayMinutes"] = disruption.DelayMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["legIndex"] = (disruption.LegIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, false, ct);

    private async Task SendAsync(Booking booking, string template, Dictionary<string, string> extra, bool withItinerary, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(booking.ContactEmail))
        {
            logger.LogInformation("No contact email for booking {BookingId}; skipping {Template}.", booking.Id, template);
            return;
        }

        try
        {
            var data = new Dictionary<string, string>(extra)
            {
                ["name"] = booking.Passengers.FirstOrDefault()?.FullName ?? "",
                ["bookingId"] = booking.Id,
                ["link"] = $"{options.Value.WebBaseUrl.TrimEnd('/')}/trips/{booking.Id}",
                ["total"] = Format(booking.TotalMinor, booking.Currency),
                ["route"] = Route(booking),
            };
            List<EmailAttachment>? attachments = null;
            if (withItinerary)
            {
                attachments = [new EmailAttachment($"dhathuru-{booking.Id}.pdf", "application/pdf", ItineraryPdf.Render(booking, t => signer.Sign(t.Id, t.Nonce, t.KeyId)))];
            }
            await sender.SendAsync(new EmailMessage(booking.ContactEmail, template, booking.Locale, data, attachments), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not send {Template} for booking {BookingId}.", template, booking.Id);
        }
    }

    internal static string Route(Booking booking)
    {
        var legs = booking.Legs.OrderBy(l => l.LegIndex).Select(l => System.Text.Json.Nodes.JsonNode.Parse(l.LegJson)!.AsObject()).ToList();
        return legs.Count == 0
            ? ""
            : $"{LegSnapshots.PlaceName(legs[0], "from")} → {LegSnapshots.PlaceName(legs[^1], "to")}";
    }

    internal static string Format(long minor, string currency) =>
        $"{currency} {minor / 100}.{minor % 100:00}";
}
