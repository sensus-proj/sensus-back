using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Middlewares;
using Sensus.Domain.Common;

namespace Sensus.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return MapError(result.Error);
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return MapError(result.Error);
    }

    public static IResult ToCreatedAtResult<TValue>(this Result<TValue> result, string uri)
    {
        if (result.IsSuccess)
        {
            return Results.Created(uri, result.Value);
        }

        return MapError(result.Error);
    }

    /// <summary>
    /// Construye un <see cref="ProblemDetails"/> de borde sin instanciarlo desde los endpoints.
    /// Los endpoints solo validan forma/autorización; el detalle de negocio vive en el dominio (AGENTS.md §7.4).
    /// </summary>
    public static IResult ToProblemResult(int statusCode, string title, string detail) =>
        Results.Problem(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        });

    private static IResult MapError(Error error)
    {
        int statusCode = ResolveStatusCode(error.Type);
        string correlationId = httpContext?.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? httpContext?.Request.Headers[CorrelationIdMiddleware.HeaderKey].FirstOrDefault()
            ?? Guid.NewGuid().ToString("D");

        ProblemDetails problemDetails = new()
        {
            Type = ResultProblemDetailsHttpResult.GetRfcType(statusCode),
            Title = string.IsNullOrWhiteSpace(error.Code) ? ResultProblemDetailsHttpResult.GetDefaultTitle(statusCode) : error.Code,
            Status = statusCode,
            Detail = error.Message,
            Instance = httpContext?.Request.Path.Value
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (error.Extensions is not null)
        {
            foreach ((string key, object? value) in error.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        return problemDetails;
    }

    public static ProblemDetails ToProblemDetails(this Result result, HttpContext? httpContext = null) =>
        result.IsFailure
            ? result.Error.ToProblemDetails(httpContext)
            : throw new InvalidOperationException("No se puede generar ProblemDetails de un resultado exitoso.");

    public static ProblemDetails ToProblemDetails<TValue>(this Result<TValue> result, HttpContext? httpContext = null) =>
        result.IsFailure
            ? result.Error.ToProblemDetails(httpContext)
            : throw new InvalidOperationException("No se puede generar ProblemDetails de un resultado exitoso.");

    private static ResultProblemDetailsHttpResult MapError(Error error)
    {
        int statusCode = ResolveStatusCode(error.Type);
        return new ResultProblemDetailsHttpResult(error, statusCode);
    }

    private static int ResolveStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status400BadRequest
    };

    private sealed class ResultProblemDetailsHttpResult : IResult
    {
        private readonly Error _error;
        private readonly int _statusCode;

        public ResultProblemDetailsHttpResult(Error error, int statusCode)
        {
            _error = error;
            _statusCode = statusCode;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ProblemDetails problemDetails = _error.ToProblemDetails(httpContext);

            httpContext.Response.StatusCode = _statusCode;
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: httpContext.RequestAborted);
        }

        internal static string GetRfcType(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
            StatusCodes.Status401Unauthorized => "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1",
            StatusCodes.Status403Forbidden => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
            StatusCodes.Status404NotFound => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
            StatusCodes.Status409Conflict => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
            StatusCodes.Status413PayloadTooLarge => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.11",
            StatusCodes.Status422UnprocessableEntity => "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
            StatusCodes.Status503ServiceUnavailable => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.4",
            _ => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
        };

        internal static string GetDefaultTitle(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status413PayloadTooLarge => "Payload Too Large",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            _ => "An error occurred"
        };
    }
}
