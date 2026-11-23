using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Comercios.Commands.CreateStoreReview;
using Sensus.Application.Comercios.Commands.ReplyStoreReview;
using Sensus.Application.Comercios.Commands.ToggleStoreReviewLike;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Comercios.Queries.GetStoreReviews;

namespace Sensus.Api.Endpoints;

public static class StoreReviewEndpoints
{
    public static IEndpointRouteBuilder MapStoreReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchants")
            .WithTags("Reseñas y Calificaciones de Comercios");

        group.MapGet("/{merchantId:guid}/reviews", async (
            Guid merchantId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStoreReviewsQuery(merchantId, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetStoreReviews")
        .WithSummary("Obtiene la calificación promedio y reseñas estilo Play Store de un comercio")
        .Produces<StoreReviewsSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{merchantId:guid}/reviews", async (
            Guid merchantId,
            [FromBody] CreateStoreReviewBody body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateStoreReviewCommand(merchantId, body.Rating, body.Comment);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateStoreReview")
        .WithSummary("Publica una calificación por estrellas (1..5) y comentario moderado para un comercio")
        .Produces<StoreReviewsSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reviews/{reviewId:guid}/like", async (
            Guid reviewId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ToggleStoreReviewLikeCommand(reviewId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("ToggleStoreReviewLike")
        .WithSummary("Alterna 'Me gusta' sobre una reseña/comentario de comercio")
        .Produces<ReviewLikeStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reviews/{reviewId:guid}/replies", async (
            Guid reviewId,
            [FromBody] ReplyStoreReviewBody body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ReplyStoreReviewCommand(reviewId, body.Comment);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("ReplyStoreReview")
        .WithSummary("Responde con texto moderado a una reseña de comercio")
        .Produces<StoreReviewDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record CreateStoreReviewBody(int Rating, string Comment);

public sealed record ReplyStoreReviewBody(string Comment);
