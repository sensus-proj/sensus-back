using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.Commands.CreateFeedComment;
using Sensus.Application.Feed.Commands.CreateFeedPost;
using Sensus.Application.Feed.Commands.DeleteFeedPost;
using Sensus.Application.Feed.Commands.SetFeedPostLike;
using Sensus.Application.Feed.DTOs;
using Sensus.Application.Feed.Queries.GetNearbyFeed;
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

        // Spec 026: Feed geolocalizado de publicaciones visibles, cronológico inverso.
        // Los parámetros se reciben como texto para responder 400 uniforme (no 500) ante valores no numéricos.
        group.MapGet("/nearby", async (
            [FromQuery] string? lat,
            [FromQuery] string? lng,
            [FromQuery] string? radiusKm,
            [FromQuery] string? page,
            [FromQuery] string? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lng))
            {
                return Result.Failure(Error.Validation(
                    "NearbyFeed.CoordinatesRequired",
                    "Las coordenadas lat y lng son obligatorias.")).ToHttpResult();
            }

            if (!TryParseOptionalCoordinate(lat, out double? latitude)
                || !TryParseOptionalCoordinate(lng, out double? longitude)
                || !TryParseOptionalCoordinate(radiusKm, out double? radius)
                || !TryParseOptionalInt(page, out int? pageNumber)
                || !TryParseOptionalInt(pageSize, out int? size))
            {
                return Result.Failure(Error.Validation(
                    "NearbyFeed.InvalidParameters",
                    "Las coordenadas, el radio y la paginación deben ser numéricos.")).ToHttpResult();
            }

            var query = new GetNearbyFeedQuery(
                latitude!.Value,
                longitude!.Value,
                radius ?? NearbyFeedPolicy.MaxRadiusKm,
                pageNumber ?? PaginationPolicy.DefaultPageNumber,
                size ?? PaginationPolicy.DefaultPageSize);

            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetNearbyFeed")
        .WithSummary("Spec 026: Feed paginado de publicaciones visibles dentro de un radio de hasta 5 km, de la más reciente a la más antigua")
        .Produces<PagedResult<NearbyFeedPostDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 024: Crear publicación en el feed social.
        // multipart/form-data permite moderar imagen y texto en la misma solicitud; JSON solo acepta mediaAssetId aprobado.
        group.MapPost("/posts", async (
            HttpContext http,
            ISender sender,
            CancellationToken ct) =>
        {
            string content = string.Empty;
            string? imageUrl = null;
            Guid? mediaAssetId = null;
            byte[]? imageBytes = null;
            string? imageContentType = null;
            double? latitude = null;
            double? longitude = null;

            if (http.Request.HasFormContentType)
            {
                IFormCollection form = await http.Request.ReadFormAsync(ct);

                // Spec 026: ubicación opcional compartida explícitamente por el autor.
                if (!TryParseOptionalCoordinate(form["latitude"].ToString(), out latitude)
                    || !TryParseOptionalCoordinate(form["longitude"].ToString(), out longitude))
                {
                    return Result.Failure(Error.Validation(
                        "FeedPost.InvalidLocation",
                        "La latitud y la longitud deben ser números válidos.")).ToHttpResult();
                }

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
                        return Result.Failure(Error.Validation(
                            "FeedPost.UnsupportedImageType",
                            "Formato de imagen no admitido. Use JPEG, PNG o WebP.")).ToHttpResult();
                    }

                    if (image.Length > MaxFeedImageBytes)
                    {
                        return Result.Failure(Error.PayloadTooLarge(
                            "FeedPost.ImageTooLarge",
                            "La imagen supera el límite de 4,5 MB.")).ToHttpResult();
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
                mediaAssetId = body?.MediaAssetId ?? body?.MediaId;
                latitude = body?.Latitude;
                longitude = body?.Longitude;
            }

            Result<FeedPostDto> result = await sender.Send(
                new CreateFeedPostCommand(content, imageUrl, imageBytes, imageContentType, latitude, longitude, mediaAssetId),
                ct);

            if (result.IsSuccess && string.Equals(
                result.Value.Status,
                Sensus.Domain.Enums.FeedPostStatus.Quarantined.ToString(),
                StringComparison.Ordinal))
            {
                return Results.Accepted($"/api/v1/feed/posts/{result.Value.Id}", result.Value);
            }

            return result.ToCreatedAtResult($"/api/v1/feed/posts/{(result.IsSuccess ? result.Value.Id : string.Empty)}");
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("CreateFeedPost")
        .WithSummary("Spec 024: Crea una publicación en el feed sujeta a moderación previa obligatoria fail-closed")
        .Produces<FeedPostDto>(StatusCodes.Status201Created)
        .Produces<FeedPostDto>(StatusCodes.Status202Accepted)
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

        // Spec 039: Me gusta en publicaciones con intención explícita (FR-039-01 a FR-039-06)
        group.MapPost("/posts/{id:guid}/like", async (
            Guid id,
            HttpContext http,
            ISender sender,
            CancellationToken ct) =>
        {
            // Lectura explícita: un cuerpo malformado o con tipo incorrecto responde 400 uniforme, nunca 500.
            SetFeedPostLikeBody? body;
            try
            {
                body = await http.Request.ReadFromJsonAsync<SetFeedPostLikeBody>(ct);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                body = null;
            }

            if (body?.Liked is not bool liked)
            {
                return Result.Failure(Error.Validation(
                    "FeedPostLike.LikedRequired",
                    "El campo liked es obligatorio y debe ser booleano.")).ToHttpResult();
            }

            var result = await sender.Send(new SetFeedPostLikeCommand(id, liked), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.PostLikesPolicy)
        .Accepts<SetFeedPostLikeBody>("application/json")
        .WithName("SetFeedPostLike")
        .WithSummary("Spec 039: Establece el me gusta del usuario sobre una publicación visible y devuelve el conteo actualizado")
        .Produces<FeedPostLikeStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
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

    private static bool TryParseOptionalCoordinate(string? raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseOptionalInt(string? raw, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}

public sealed record SetFeedPostLikeBody(bool? Liked = null);

public sealed record CreateFeedCommentBody(string? Content = null, string? Text = null);

public sealed record CreateFeedPostBody(
    string? Content = null,
    string? Text = null,
    string? ImageUrl = null,
    double? Latitude = null,
    double? Longitude = null,
    Guid? MediaAssetId = null,
    Guid? MediaId = null);
