using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Sanctions.Commands.CreateManualSanction;
using Sensus.Application.Sanctions.Commands.RevertSanction;
using Sensus.Application.Sanctions.Common;
using Sensus.Application.Sanctions.Queries;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Spec 020: consulta, sanción manual y reversión de sanciones. Todas las rutas son exclusivas del rol
/// Administrador (Principio VIII); la sanción automática no tiene superficie HTTP.
/// </summary>
public static class SanctionEndpoints
{
    public const string RoutePrefix = "/api/v1/moderation/sanctions";

    public static IEndpointRouteBuilder MapSanctionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup(RoutePrefix)
            .WithTags("Moderación: sanciones y baneo")
            .RequireAuthorization(SecurityExtensions.AdminOnlyPolicy);

        group.MapGet("/", async (
            [FromQuery] Guid? userId,
            [FromQuery] string? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ListSanctionsQuery(userId, status, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("ListSanctions")
        .WithSummary("Spec 020 (FR-020-05): lista paginada de sanciones")
        .Produces<PagedResult<SanctionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSanctionQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetSanction")
        .WithSummary("Spec 020: detalle de una sanción")
        .Produces<SanctionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/", async (
            [FromBody] CreateSanctionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateManualSanctionCommand(body.UserId, body.Reason, body.DurationHours),
                ct);
            return result.ToCreatedAtResult($"{RoutePrefix}/{(result.IsSuccess ? result.Value.Id : Guid.Empty)}");
        })
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateManualSanction")
        .WithSummary("Spec 020 (FR-020-07): aplica una sanción manual, temporal o permanente")
        .Produces<SanctionDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{id:guid}/revert", async (
            Guid id,
            [FromBody] RevertSanctionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new RevertSanctionCommand(id, body.Reason), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("RevertSanction")
        .WithSummary("Spec 020 (FR-020-02): revierte una sanción activa")
        .Produces<SanctionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    public sealed record CreateSanctionRequest(Guid UserId, string Reason, int? DurationHours);

    public sealed record RevertSanctionRequest(string Reason);
}
