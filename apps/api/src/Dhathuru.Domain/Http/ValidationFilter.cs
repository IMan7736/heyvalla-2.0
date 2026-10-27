using FluentValidation;

namespace Dhathuru.Domain.Http;

/// <summary>
/// Endpoint filter that validates the first argument of type <typeparamref name="T"/> with its FluentValidation validator.
/// Failures become a 422 problem whose field errors carry the validator's error code (set with <c>WithErrorCode</c>).
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            throw new ProblemException(StatusCodes.Status400BadRequest, "BAD_REQUEST");
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            throw ProblemException.Validation([.. result.Errors.Select(e => new FieldError(
                Camel(e.PropertyName),
                string.IsNullOrEmpty(e.ErrorCode) ? "INVALID" : e.ErrorCode,
                (e.FormattedMessagePlaceholderValues ?? new Dictionary<string, object>())
                    .Where(p => p.Key is not ("PropertyName" or "PropertyValue" or "PropertyPath" or "CollectionIndex"))
                    .ToDictionary(p => Camel(p.Key), p => (object?)p.Value)))]);
        }

        return await next(context);
    }

    private static string Camel(string value) =>
        value.Length == 0 ? value : string.Concat(char.ToLowerInvariant(value[0]).ToString(), value.AsSpan(1));
}

public static class ValidationExtensions
{
    public static TBuilder Validate<TBuilder, T>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        where T : class =>
        builder.AddEndpointFilter(new ValidationFilter<T>());
}
