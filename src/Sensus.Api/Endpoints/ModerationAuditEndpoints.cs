using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Moderation.Audit;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Consulta administrativa de la auditoría de moderación (Spec 021).
/// Toda la superficie exige JWT con rol Administrador (CA-021-03, Principio IV).
/// </summary>
public static class ModerationAuditEndpoints
{
    public const string RoutePrefix = "/api/v1/admin/moderation/audit";

    /// <summary>Ref: FR-021-03 - consulta paginada por administrador.</summary>
    public static IEndpointRouteBuilder MapModerationAuditEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup(RoutePrefix)
            .WithTags("Moderación: auditoría (Spec 021)")
            .RequireAuthorization(SecurityExtensions.AdminOnlyPolicy);

        group.MapGet("/", async (
            [FromQuery] Guid? targetId,
            [FromQuery] string? targetType,
            [FromQuery] string? verdict,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetModerationAuditQuery(
                    targetId,
                    targetType,
                    verdict,
                    fromUtc,
                    toUtc,
                    page ?? PaginationPolicy.DefaultPageNumber,
                    pageSize ?? PaginationPolicy.DefaultPageSize),
                ct);

            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetModerationAudit")
        .WithSummary("Spec 021 (FR-021-03): consulta paginada e inmutable de las decisiones de moderación")
        .Produces<PagedResult<ModerationAuditRecordDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
