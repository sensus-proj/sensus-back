using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.Commands.CreateFeedComment;
using Sensus.Application.Feed.DTOs;
using Sensus.Application.Feed.Queries.GetPostComments;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetOfferFeed;
using Sensus.Domain.Common;

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
                return Result.Failure(Error.Validation(
                    "OfferFeed.CoordinatesRequired",
                    "Las coordenadas lat y lng son obligatorias.")).ToHttpResult();
            }

            Guid? parsedSnapshotId = null;
            if (!string.IsNullOrWhiteSpace(snapshotId))
            {
                if (!Guid.TryParse(snapshotId.Trim(), out Guid parsed) || parsed == Guid.Empty)
                {
                    return Result.Failure(Error.Validation(
                        "OfferFeed.InvalidSnapshotId",
                        "El parámetro snapshotId debe tener formato GUID válido.")).ToHttpResult();
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

        // Spec 025: Comentarios moderados en publicaciones del feed
        group.MapPost("/posts/{id:guid}/comments", async (
            Guid id,
            [FromBody] CreateFeedCommentBody body,
            ISender sender,
            CancellationToken ct) =>
        {
            string text = body.Content ?? body.Text ?? string.Empty;
            var result = await sender.Send(new CreateFeedCommentCommand(id, text), ct);
            return result.ToCreatedAtResult($"/api/v1/feed/posts/{id}/comments");
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateFeedPostComment")
        .WithSummary("Spec 025: Publica un comentario moderado en una publicación del feed")
        .Produces<FeedCommentDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/posts/{id:guid}/comments", async (
            Guid id,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new GetPostCommentsQuery(
                    id,
                    page ?? PaginationPolicy.DefaultPageNumber,
                    pageSize ?? PaginationPolicy.DefaultPageSize),
                ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetFeedPostComments")
        .WithSummary("Spec 025: Lista comentarios paginados de una publicación ordenados cronológicamente")
        .Produces<PagedResult<FeedCommentDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}

public sealed record CreateFeedCommentBody(string? Content = null, string? Text = null);
