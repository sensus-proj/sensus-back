using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetOfferFeed;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Endpoints del feed de promociones por intereses (Spec 042).
/// </summary>
public static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/feed")
            .WithTags("Feed de promociones (Spec 042)");

        group.MapGet("/offers", async (
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] int? radiusKm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? snapshotId,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!lat.HasValue || !lng.HasValue)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "OfferFeed.CoordinatesRequired",
                    Detail = "Las coordenadas lat y lng son obligatorias."
                });
            }

            Guid? parsedSnapshotId = null;
            if (!string.IsNullOrWhiteSpace(snapshotId))
            {
                if (!Guid.TryParse(snapshotId.Trim(), out Guid parsed) || parsed == Guid.Empty)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "OfferFeed.InvalidSnapshotId",
                        Detail = "El parámetro snapshotId debe tener formato GUID válido."
                    });
                }

                parsedSnapshotId = parsed;
            }

            var query = new GetOfferFeedQuery(
                lat.Value,
                lng.Value,
                radiusKm ?? 10,
                page ?? 1,
                pageSize ?? 20,
                parsedSnapshotId);

            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetOfferFeed")
        .WithSummary("Spec 042: Feed paginado de ofertas cercanas ordenado por intereses, afinidad, cercanía, urgencia y popularidad")
        .Produces<OfferFeedPageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
