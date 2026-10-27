namespace Dhathuru.Domain.Http;

/// <summary>One field level validation error. <c>Code</c> is stable and translated by the client.</summary>
public sealed record FieldError(string Field, string Code, IReadOnlyDictionary<string, object?>? Params = null);

/// <summary>
/// Throw from any module to answer with an RFC 9457 problem+json that carries a stable machine <c>code</c>.
/// The host turns it into the response; handlers never write error JSON by hand.
/// </summary>
public class ProblemException(int status, string code, IReadOnlyDictionary<string, object?>? parameters = null, IReadOnlyList<FieldError>? errors = null)
    : Exception(code)
{
    public int Status { get; } = status;

    public string Code { get; } = code;

    public IReadOnlyDictionary<string, object?> Params { get; } = parameters ?? new Dictionary<string, object?>();

    public IReadOnlyList<FieldError> Errors { get; } = errors ?? [];

    public static ProblemException NotFound(string code = "NOT_FOUND") => new(StatusCodes.Status404NotFound, code);

    public static ProblemException Unauthorized(string code = "AUTH_REQUIRED") => new(StatusCodes.Status401Unauthorized, code);

    public static ProblemException Forbidden(string code = "FORBIDDEN") => new(StatusCodes.Status403Forbidden, code);

    public static ProblemException Conflict(string code, IReadOnlyDictionary<string, object?>? parameters = null) =>
        new(StatusCodes.Status409Conflict, code, parameters);

    public static ProblemException Validation(params FieldError[] errors) =>
        new(StatusCodes.Status422UnprocessableEntity, "VALIDATION_FAILED", null, errors);
}
