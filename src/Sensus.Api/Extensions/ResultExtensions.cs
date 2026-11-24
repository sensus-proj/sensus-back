using Microsoft.AspNetCore.Mvc;
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
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message
        };

        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        return Results.Problem(problemDetails);
    }
}
