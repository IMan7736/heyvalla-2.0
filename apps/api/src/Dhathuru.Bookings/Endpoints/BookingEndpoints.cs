using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Services;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Endpoints;

public sealed record PassengerBody(string? TravellerId, string? FullName, string? PassengerType, string? Phone);

public sealed record CreateBookingBody(string? OptionId, List<PassengerBody>? Passengers);

public sealed record PayBody(string? Method, string? PaymentToken);

public sealed record VerificationBody(string? Code);

public sealed record VerifyTicketBody(string? Payload, bool Consume = false);

public sealed record ProviderEventBody(string? ProviderId, string? ServiceRunId, string? State, int DelayMinutes = 0);

public static class BookingEndpoints
{
    /// <summary>
    /// Maps the Bookings routes. The caller (Api host) applies the <c>/v1</c> group; authorisation is required on every route except ticket verification.
    /// </summary>
    public static IEndpointRouteBuilder MapBookingsEndpoints(this IEndpointRouteBuilder v1)
    {
        // No RequireRateLimiting here: the host applies its global per-user limiter to every route, and only "auth" exists as a named policy.
        var bookings = v1.MapGroup("/bookings").WithTags("Bookings").RequireAuthorization();

        bookings.MapPost("/", async (CreateBookingBody body, HttpContext http, BookingService service, CancellationToken ct) =>
        {
            var key = IdempotencyKey(http);
            var command = new CreateBookingCommand(
                body.OptionId ?? "",
                [.. (body.Passengers ?? []).Select(p => new PassengerInput(p.TravellerId, p.FullName ?? "", p.PassengerType ?? "", p.Phone))]);
            var created = await service.CreateAsync(CurrentUser.From(http.User), key, command, ct);
            return Results.Created($"/v1/bookings/{created.Id}", created);
        }).RequireIdempotency().RequireAuthorization(Dhathuru.Domain.Security.Policies.VerifiedEmail);

        bookings.MapGet("/", async (string? cursor, int? limit, string? status, HttpContext http, BookingService service, CancellationToken ct) =>
        {
            BookingStatus? parsed = null;
            if (status is not null)
            {
                if (!WireNames.TryParse(status, out var s))
                {
                    throw ProblemException.Validation(new FieldError("status", "INVALID_STATUS"));
                }
                parsed = s;
            }
            return Results.Ok(await service.ListAsync(CurrentUser.From(http.User), cursor, limit ?? 50, parsed, ct));
        });

        bookings.MapGet("/{bookingId}", async (string bookingId, HttpContext http, BookingService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(CurrentUser.From(http.User), bookingId, ct)));

        bookings.MapPost("/{bookingId}/pay", async (string bookingId, PayBody body, HttpContext http, BookingService service, CancellationToken ct) =>
            Results.Ok(await service.PayAsync(CurrentUser.From(http.User), bookingId, IdempotencyKey(http), new PayCommand(body.Method ?? "", body.PaymentToken ?? ""), ct)))
            .RequireIdempotency().RequireAuthorization(Dhathuru.Domain.Security.Policies.VerifiedEmail);

        bookings.MapPost("/{bookingId}/pay/verify", async (string bookingId, VerificationBody body, HttpContext http, BookingService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Code) || body.Code.Length > 12)
            {
                throw ProblemException.Validation(new FieldError("code", "CODE_REQUIRED"));
            }
            return Results.Ok(await service.CompleteChallengeAsync(CurrentUser.From(http.User), bookingId, body.Code, ct));
        }).RequireAuthorization(Dhathuru.Domain.Security.Policies.VerifiedEmail);

        bookings.MapGet("/{bookingId}/cancellation-quote", async (string bookingId, HttpContext http, BookingService service, CancellationToken ct) =>
            Results.Ok(await service.QuoteCancellationAsync(CurrentUser.From(http.User), bookingId, ct)));

        bookings.MapPost("/{bookingId}/cancel", async (string bookingId, HttpContext http, BookingService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(CurrentUser.From(http.User), bookingId, ct)));

        bookings.MapGet("/{bookingId}/tickets", async (string bookingId, HttpContext http, BookingService service, CancellationToken ct) =>
            Results.Ok(new { items = await service.TicketsAsync(CurrentUser.From(http.User), bookingId, ct) }));

        bookings.MapGet("/{bookingId}/itinerary.pdf", async (string bookingId, HttpContext http, BookingService service, Tickets.TicketSigner signer, CancellationToken ct) =>
        {
            var booking = await service.GetForItineraryAsync(CurrentUser.From(http.User), bookingId, ct);
            var pdf = ItineraryPdf.Render(booking, t => signer.Sign(t.Id, t.Nonce, t.KeyId));
            return Results.File(pdf, "application/pdf", $"dhathuru-{booking.Id}.pdf");
        });

        bookings.MapPost("/{bookingId}/disruptions/{disruptionId}/acknowledge", async (string bookingId, string disruptionId, HttpContext http, DisruptionService service, CancellationToken ct) =>
            Results.Ok(await service.AcknowledgeAsync(CurrentUser.From(http.User), bookingId, disruptionId, ct)));

        var tickets = v1.MapGroup("/tickets").WithTags("Bookings");

        tickets.MapGet("/{ticketId}/qr", async (string ticketId, HttpContext http, TicketService service, CancellationToken ct) =>
            Results.Content(await service.QrSvgAsync(CurrentUser.From(http.User), ticketId, ct), "image/svg+xml"))
            .RequireAuthorization();

        tickets.MapPost("/verify", async (VerifyTicketBody body, HttpContext http, TicketService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Payload) || body.Payload.Length > 256)
            {
                throw ProblemException.Validation(new FieldError("payload", "PAYLOAD_REQUIRED"));
            }
            var result = await service.VerifyAsync(body.Payload, body.Consume, http.Request.Headers["X-Operator-Key"].FirstOrDefault(), ct);
            return Results.Ok(new { valid = result.Valid, reasonCode = result.ReasonCode, ticket = result.Ticket });
        });

        // Development only: lets the web app and tests script a provider delay or cancellation.
        v1.MapPost("/dev/provider-events", async (ProviderEventBody body, HttpContext http, DisruptionService service, IOptions<BookingsOptions> options, CancellationToken ct) =>
        {
            if (!options.Value.EnableSimulation)
            {
                return Results.NotFound();
            }
            if (string.IsNullOrWhiteSpace(body.ProviderId) || string.IsNullOrWhiteSpace(body.ServiceRunId)
                || !Enum.TryParse<ServiceRunState>(body.State?.Replace("_", ""), ignoreCase: true, out var state))
            {
                throw ProblemException.Validation(new FieldError("state", "INVALID_STATE"));
            }
            var affected = await service.ReportAsync(body.ProviderId, body.ServiceRunId, state, body.DelayMinutes, ct);
            return Results.Ok(new { affected });
        }).RequireAuthorization();

        return v1;
    }

    private static string IdempotencyKey(HttpContext http)
    {
        var key = http.Request.Headers[IdempotencyExtensions.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            throw ProblemException.Validation(new FieldError("Idempotency-Key", "IDEMPOTENCY_KEY_REQUIRED"));
        }
        return key;
    }
}

/// <summary>Picked up by the Api host (<see cref="Dhathuru.Domain.Http.IEndpointModule"/>), which maps it under <c>/v1</c>.</summary>
public sealed class BookingsEndpointModule : IEndpointModule
{
    public void MapEndpoints(RouteGroupBuilder v1) => v1.MapBookingsEndpoints();
}
