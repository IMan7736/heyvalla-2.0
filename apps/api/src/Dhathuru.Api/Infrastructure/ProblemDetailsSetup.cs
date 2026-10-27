using Dhathuru.Api.Middleware;
using Dhathuru.Domain.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace Dhathuru.Api.Infrastructure;

public static class ProblemDetailsSetup
{
    /// <summary>
    /// RFC 9457 problem+json everywhere, with a stable machine <c>code</c> and <c>params</c>.
    /// Clients translate the code; no English prose is required from the API.
    /// </summary>
    public static IServiceCollection AddDhathuruProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            problem.Status = status;
            var exception = context.Exception as ProblemException ?? context.HttpContext.Items[ProblemItemKey] as ProblemException;
            var code = exception?.Code ?? CodeFor(status);
            problem.Type = $"https://dhathuru.mv/problems/{code.ToLowerInvariant().Replace('_', '-')}";
            problem.Extensions["code"] = code;
            if (exception is not null || !problem.Extensions.ContainsKey("params"))
            {
                problem.Extensions["params"] = exception?.Params ?? new Dictionary<string, object?>();
            }
            if (exception is { Errors.Count: > 0 })
            {
                problem.Extensions["errors"] = exception.Errors.Select(e => new
                {
                    field = e.Field,
                    code = e.Code,
                    @params = e.Params ?? new Dictionary<string, object?>(),
                });
            }

            problem.Extensions["traceId"] = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.TraceId.ToString()
                                            ?? context.HttpContext.TraceIdentifier;
            if (CorrelationIdMiddleware.Get(context.HttpContext) is { } correlationId)
            {
                problem.Extensions["correlationId"] = correlationId;
            }

            problem.Title = TitleFor(status);
        });
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    internal const string ProblemItemKey = "dhathuru.problem";

    public static string CodeFor(int status) => status switch
    {
        400 => "BAD_REQUEST",
        401 => "AUTH_REQUIRED",
        403 => "FORBIDDEN",
        404 => "NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        409 => "CONFLICT",
        410 => "GONE",
        413 => "PAYLOAD_TOO_LARGE",
        415 => "UNSUPPORTED_MEDIA_TYPE",
        422 => "VALIDATION_FAILED",
        429 => "RATE_LIMITED",
        >= 500 => "INTERNAL_ERROR",
        _ => "ERROR",
    };

    private static string TitleFor(int status) => status switch
    {
        400 => "Bad request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not found",
        409 => "Conflict",
        422 => "Validation failed",
        429 => "Too many requests",
        >= 500 => "Internal server error",
        _ => "Error",
    };

    /// <summary>Turns <see cref="ProblemException"/> into its problem response; anything else is a logged 500 with no detail leaked.</summary>
    private sealed class UnhandledExceptionHandler(IProblemDetailsService problems, ILogger<UnhandledExceptionHandler> logger)
        : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is ProblemException problem)
            {
                httpContext.Response.StatusCode = problem.Status;
                return await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = problem,
                    ProblemDetails = { Status = problem.Status },
                });
            }

            if (exception is BadHttpRequestException bad)
            {
                // Malformed JSON, unreadable body, oversized payload: the client's mistake, not ours.
                httpContext.Response.StatusCode = bad.StatusCode;
                return await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = { Status = bad.StatusCode },
                });
            }

            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                return true; // the client went away; nothing to answer
            }

            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = { Status = StatusCodes.Status500InternalServerError },
            });
        }
    }
}
