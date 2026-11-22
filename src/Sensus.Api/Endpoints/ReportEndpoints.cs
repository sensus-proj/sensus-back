using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Reports.Commands.CreateContentReport;
using Sensus.Application.Reports.DTOs;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Endpoints de reportes comunitarios de contenido (Spec 040).
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/reports")
            .WithTags("Reportes de contenido (Spec 040)");

        group.MapPost("/", async (
            [FromBody] CreateContentReportBody body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateContentReportCommand(
                body.TargetType,
                body.TargetId,
                body.Reason,
                body.Details);

            var result = await sender.Send(command, ct);
            return result.ToCreatedAtResult($"/api/v1/reports/{(result.IsSuccess ? result.Value.Id : Guid.Empty)}");
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateContentReport")
        .WithSummary("Spec 040: Registra un reporte comunitario de contenido con deduplicación en 24 h")
        .Produces<ContentReportDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}

public sealed record CreateContentReportBody(
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details);
