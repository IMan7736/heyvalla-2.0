using Dhathuru.Domain.Pricing;

namespace Dhathuru.Bookings.Payments;

/// <summary>
/// Payment seam used by the booking saga. Card numbers never reach this process: callers pass an opaque
/// token minted by the gateway's hosted fields. See docs/payments/GATEWAY-READINESS.md for what a real
/// Maldives gateway (BML, MIB, Ooredoo m-faisaa) must implement.
/// </summary>
public interface IPaymentGateway
{
    string GatewayId { get; }

    /// <summary>Authorises and captures. Idempotent on <see cref="ChargeRequest.IdempotencyKey"/>.</summary>
    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);

    /// <summary>Completes an extra verification step (3-D Secure style) started by a charge.</summary>
    Task<ChargeResult> CompleteChallengeAsync(CompleteChallengeRequest request, CancellationToken ct);

    /// <summary>Finds what happened to a charge when the response was lost (timeouts). Null if the gateway never saw it.</summary>
    Task<ChargeResult?> LookupAsync(string idempotencyKey, CancellationToken ct);

    /// <summary>Idempotent on <see cref="RefundRequest.IdempotencyKey"/>.</summary>
    Task<RefundOutcome> RefundAsync(RefundRequest request, CancellationToken ct);
}

public sealed record ChargeRequest(string BookingId, Money Amount, string PaymentToken, string IdempotencyKey);

public sealed record CompleteChallengeRequest(string GatewayReference, string ChallengeId, string Code);

public enum ChargeOutcome { Succeeded, RequiresAction, Declined }

public sealed record ChargeResult(ChargeOutcome Outcome, string? GatewayReference = null, string? FailureCode = null, string? ChallengeId = null);

public sealed record RefundRequest(string GatewayReference, Money Amount, string IdempotencyKey);

public sealed record RefundOutcome(bool Success, string? FailureCode = null);

/// <summary>Stable machine codes returned by the payment flow (the web owns the English text).</summary>
public static class PaymentFailureCodes
{
    public const string CardDeclined = "CARD_DECLINED";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string VerificationFailed = "VERIFICATION_FAILED";
    public const string PaymentTimeout = "PAYMENT_TIMEOUT";
    public const string GatewayError = "GATEWAY_ERROR";
    public const string InvalidToken = "INVALID_PAYMENT_TOKEN";
}
