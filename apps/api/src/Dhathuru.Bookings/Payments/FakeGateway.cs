using System.Collections.Concurrent;
using System.Security.Cryptography;
using Dhathuru.Domain.Pricing;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Payments;

/// <summary>Tokens the fake gateway understands. The web maps documented test card numbers to these (see docs/payments).</summary>
public static class FakeTokens
{
    public const string Success = "tok_fake_success";
    public const string Declined = "tok_fake_declined";
    public const string InsufficientFunds = "tok_fake_insufficient_funds";
    public const string Verification = "tok_fake_verification";
    public const string Slow = "tok_fake_slow";
    public const string Timeout = "tok_fake_timeout";
    public const string TimeoutCharged = "tok_fake_timeout_charged";
    /// <summary>Charge succeeds, then the saga's fault injector fails the last leg's confirmation.</summary>
    public const string LegFailure = "tok_fake_leg_failure";

    public const string VerificationCode = "123456";
}

public sealed class FakeGatewayOptions
{
    public TimeSpan SlowDelay { get; set; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Deterministic in-memory gateway for development and tests. It is honest about being fake: references start
/// with <c>fake_</c> and nothing here talks to a real network or processor.
/// </summary>
public sealed class FakeGateway(IOptions<FakeGatewayOptions> options) : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, ChargeResult> _charges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _challenges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _refunds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _refunded = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _captured = new(StringComparer.Ordinal);

    public string GatewayId => "fake";

    public async Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        if (_charges.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return existing;
        }

        ChargeResult result;
        switch (request.PaymentToken)
        {
            case FakeTokens.Success or FakeTokens.LegFailure:
                result = Captured(request);
                break;
            case FakeTokens.Slow:
                await Task.Delay(options.Value.SlowDelay, ct).ConfigureAwait(false);
                result = Captured(request);
                break;
            case FakeTokens.Declined:
                result = new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.CardDeclined);
                break;
            case FakeTokens.InsufficientFunds:
                result = new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.InsufficientFunds);
                break;
            case FakeTokens.Verification:
                var reference = NewReference();
                var challenge = "chl_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
                _challenges[challenge] = reference;
                _captured[reference] = request.Amount.AmountMinor;
                result = new ChargeResult(ChargeOutcome.RequiresAction, reference, ChallengeId: challenge);
                break;
            case FakeTokens.Timeout:
                // The request never reaches the processor: wait until the caller gives up.
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");
            case FakeTokens.TimeoutCharged:
                // The processor takes the money but the response is lost.
                _charges[request.IdempotencyKey] = Captured(request);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");
            default:
                result = new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.InvalidToken);
                break;
        }

        return _charges.GetOrAdd(request.IdempotencyKey, result);
    }

    public Task<ChargeResult> CompleteChallengeAsync(CompleteChallengeRequest request, CancellationToken ct)
    {
        if (!_challenges.TryGetValue(request.ChallengeId, out var reference) || reference != request.GatewayReference)
        {
            return Task.FromResult(new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.VerificationFailed));
        }

        _challenges.TryRemove(request.ChallengeId, out _);
        if (!string.Equals(request.Code, FakeTokens.VerificationCode, StringComparison.Ordinal))
        {
            _captured.TryRemove(reference, out _);
            return Task.FromResult(new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.VerificationFailed));
        }

        return Task.FromResult(new ChargeResult(ChargeOutcome.Succeeded, reference));
    }

    public Task<ChargeResult?> LookupAsync(string idempotencyKey, CancellationToken ct) =>
        Task.FromResult(_charges.TryGetValue(idempotencyKey, out var result) ? result : null);

    public Task<RefundOutcome> RefundAsync(RefundRequest request, CancellationToken ct)
    {
        if (!_captured.TryGetValue(request.GatewayReference, out var captured))
        {
            return Task.FromResult(new RefundOutcome(false, PaymentFailureCodes.GatewayError));
        }

        if (!_refunds.TryAdd(request.IdempotencyKey, 0))
        {
            return Task.FromResult(new RefundOutcome(true));
        }

        lock (_refunded)
        {
            var already = _refunded.GetValueOrDefault(request.GatewayReference);
            if (already + request.Amount.AmountMinor > captured)
            {
                _refunds.TryRemove(request.IdempotencyKey, out _);
                return Task.FromResult(new RefundOutcome(false, PaymentFailureCodes.GatewayError));
            }
            _refunded[request.GatewayReference] = already + request.Amount.AmountMinor;
        }

        return Task.FromResult(new RefundOutcome(true));
    }

    /// <summary>Total refunded for a reference. Test helper to prove money stays consistent.</summary>
    public long RefundedMinor(string gatewayReference) => _refunded.GetValueOrDefault(gatewayReference);

    public long CapturedMinor(string gatewayReference) => _captured.GetValueOrDefault(gatewayReference);

    private ChargeResult Captured(ChargeRequest request)
    {
        var reference = NewReference();
        _captured[reference] = request.Amount.AmountMinor;
        return new ChargeResult(ChargeOutcome.Succeeded, reference);
    }

    private static string NewReference() => "fake_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
}
