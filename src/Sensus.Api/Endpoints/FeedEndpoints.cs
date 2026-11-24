using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.Commands.CreateFeedComment;
using Sensus.Application.Feed.Commands.CreateFeedPost;
using Sensus.Application.Feed.Commands.DeleteFeedPost;
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
    // Límite de la spec 022 (4,5 MB). El endpoint JSON admite imágenes ya moderadas por ese flujo.
    private const long MaxFeedImageBytes = 4_500_000;

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
                return ResultExtensions.ToProblemResult(
                    StatusCodes.Status400BadRequest,
                    "OfferFeed.CoordinatesRequired",
                    "Las coordenadas lat y lng son obligatorias.");
            }

            Guid? parsedSnapshotId = null;
            if (!string.IsNullOrWhiteSpace(snapshotId))
            {
                if (!Guid.TryParse(snapshotId.Trim(), out Guid parsed) || parsed == Guid.Empty)
                {
                    return ResultExtensions.ToProblemResult(
                        StatusCodes.Status400BadRequest,
                        "OfferFeed.InvalidSnapshotId",
                        "El parámetro snapshotId debe tener formato GUID válido.");
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

        // Spec 024: Crear publicación en el feed social.
        // multipart/form-data permite moderar imagen y texto en la misma solicitud; JSON admite una URL ya moderada.
        group.MapPost("/posts", async (
            HttpContext http,
            ISender sender,
            CancellationToken ct) =>
        {
            string content = string.Empty;
            string? imageUrl = null;
            byte[]? imageBytes = null;
            string? imageContentType = null;

            if (http.Request.HasFormContentType)
            {
                IFormCollection form = await http.Request.ReadFormAsync(ct);
                content = form["content"].ToString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = form["text"].ToString();
                }

                IFormFile? image = form.Files.GetFile("image");
                if (image is { Length: > 0 })
                {
                    // Lista de permitidos (AGENTS.md §8.1): solo formatos soportados por el storage.
                    string normalizedContentType = (string.IsNullOrWhiteSpace(image.ContentType)
                            ? "image/jpeg"
                            : image.ContentType)
                        .Trim()
                        .ToLowerInvariant();

                    if (normalizedContentType is not ("image/jpeg" or "image/png" or "image/webp"))
                    {
                        return ResultExtensions.ToProblemResult(
                            StatusCodes.Status400BadRequest,
                            "FeedPost.UnsupportedImageType",
                            "Formato de imagen no admitido. Use JPEG, PNG o WebP.");
                    }

                    if (image.Length > MaxFeedImageBytes)
                    {
                        return ResultExtensions.ToProblemResult(
                            StatusCodes.Status413PayloadTooLarge,
                            "FeedPost.ImageTooLarge",
                            "La imagen supera el límite de 4,5 MB.");
                    }

                    using var buffer = new MemoryStream();
                    await image.CopyToAsync(buffer, ct);
                    imageBytes = buffer.ToArray();
                    imageContentType = normalizedContentType;
                }
            }
            else
            {
                CreateFeedPostBody? body = await http.Request.ReadFromJsonAsync<CreateFeedPostBody>(cancellationToken: ct);
                content = body?.Content ?? body?.Text ?? string.Empty;
                imageUrl = body?.ImageUrl;
            }

            Result<FeedPostDto> result = await sender.Send(
                new CreateFeedPostCommand(content, imageUrl, imageBytes, imageContentType),
                ct);

            return result.ToCreatedAtResult($"/api/v1/feed/posts/{(result.IsSuccess ? result.Value.Id : string.Empty)}");
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateFeedPost")
        .WithSummary("Spec 024: Crea una publicación en el feed sujeta a moderación previa obligatoria fail-closed")
        .Produces<FeedPostDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 024: Eliminar publicación del feed
        group.MapDelete("/posts/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteFeedPostCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("DeleteFeedPost")
        .WithSummary("Spec 024: Elimina una publicación del feed (solo autor original o administrador)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
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

public sealed record CreateFeedPostBody(string? Content = null, string? Text = null, string? ImageUrl = null);
