namespace Dhathuru.Domain.Http;

/// <summary>Endpoint metadata read by the API host's idempotency middleware.</summary>
public sealed record IdempotencyRequiredMetadata;

public static class IdempotencyExtensions
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>
    /// Marks an endpoint as requiring an <c>Idempotency-Key</c> header. The host stores the key with a hash of the
    /// request and the response; a retry replays the stored response, and reusing a key with a different request is a 409.
    /// </summary>
    public static TBuilder RequireIdempotency<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new IdempotencyRequiredMetadata());
}
