using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Queries.GetNearbyFeed;

/// <summary>
/// Feed paginado de publicaciones visibles cercanas, en orden cronológico inverso (Spec 026, FR-026-01).
/// </summary>
public sealed record GetNearbyFeedQuery(
    double Latitude,
    double Longitude,
    double RadiusKm = NearbyFeedPolicy.MaxRadiusKm,
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize) : IRequest<Result<PagedResult<NearbyFeedPostDto>>>, ISensitiveRequest;

/// <summary>
/// Reglas del feed cercano fijadas por la spec 026.
/// </summary>
public static class NearbyFeedPolicy
{
    /// <summary>FR-026-04: radio máximo de 5 km inclusive.</summary>
    public const double MaxRadiusKm = 5d;

    /// <summary>
    /// Precisión de las coordenadas para la consulta y la clave de caché (~11 m), de modo que peticiones
    /// prácticamente idénticas compartan la misma página en caché (FR-026-06).
    /// </summary>
    public const int CoordinateDecimals = 4;
}

public sealed class GetNearbyFeedQueryValidator : AbstractValidator<GetNearbyFeedQuery>
{
    public GetNearbyFeedQueryValidator()
    {
        RuleFor(x => x.Latitude)
            .Must(lat => double.IsFinite(lat) && lat is >= -90 and <= 90)
            .WithMessage("La latitud debe ser un número entre -90 y 90.");

        RuleFor(x => x.Longitude)
            .Must(lng => double.IsFinite(lng) && lng is >= -180 and <= 180)
            .WithMessage("La longitud debe ser un número entre -180 y 180.");

        RuleFor(x => x.RadiusKm)
            .Must(r => double.IsFinite(r) && r > 0 && r <= NearbyFeedPolicy.MaxRadiusKm)
            .WithMessage($"El radio debe ser mayor que 0 y como máximo {NearbyFeedPolicy.MaxRadiusKm} km.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser al menos 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationPolicy.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationPolicy.MaxPageSize}.");
    }
}

public sealed class GetNearbyFeedQueryHandler
    : IRequestHandler<GetNearbyFeedQuery, Result<PagedResult<NearbyFeedPostDto>>>
{
    private readonly IFeedPostRepository _postRepository;
    private readonly ICacheService? _cacheService;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IFeedPostLikeRepository? _likeRepository;
    private readonly IFeedCommentRepository? _commentRepository;
    private readonly ILogger<GetNearbyFeedQueryHandler> _logger;

    public GetNearbyFeedQueryHandler(
        IFeedPostRepository postRepository,
        ILogger<GetNearbyFeedQueryHandler> logger,
        ICacheService? cacheService = null,
        ICurrentUserService? currentUserService = null,
        IFeedPostLikeRepository? likeRepository = null,
        IFeedCommentRepository? commentRepository = null)
    {
        _postRepository = postRepository;
        _logger = logger;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _likeRepository = likeRepository;
        _commentRepository = commentRepository;
    }

    public async Task<Result<PagedResult<NearbyFeedPostDto>>> Handle(
        GetNearbyFeedQuery request,
        CancellationToken cancellationToken)
    {
        double latitude = Math.Round(request.Latitude, NearbyFeedPolicy.CoordinateDecimals);
        double longitude = Math.Round(request.Longitude, NearbyFeedPolicy.CoordinateDecimals);
        string cacheKey = FeedCacheKeys.Nearby(latitude, longitude, request.RadiusKm, request.Page, request.PageSize);

        NearbyFeedCacheEntry? entry = _cacheService is not null
            ? await _cacheService.GetAsync<NearbyFeedCacheEntry>(cacheKey, cancellationToken)
            : null;

        if (entry is not null)
        {
            _logger.LogDebug("Feed cercano servido desde caché (página {Page})", request.Page);
            return Result.Success(await EnrichPageAsync(entry, request, cancellationToken));
        }

        (IReadOnlyList<FeedPost> posts, int totalCount) = await _postRepository.GetPublishedNearbyPagedAsync(
            latitude,
            longitude,
            request.RadiusKm * 1000d,
            request.Page,
            request.PageSize,
            cancellationToken);

        entry = new NearbyFeedCacheEntry(posts.Select(p => p.ToNearbyDto()).ToList(), totalCount);

        if (_cacheService is not null)
        {
            await _cacheService.SetForEntityAsync(cacheKey, entry, FeedCacheKeys.NearbyCacheEntity, cancellationToken);
        }

        _logger.LogDebug("Feed cercano consultado en base de datos (página {Page}, total {Total})", request.Page, totalCount);
        return Result.Success(await EnrichPageAsync(entry, request, cancellationToken));
    }

    private async Task<PagedResult<NearbyFeedPostDto>> EnrichPageAsync(
        NearbyFeedCacheEntry entry,
        GetNearbyFeedQuery request,
        CancellationToken cancellationToken)
    {
        if (entry.Items.Count == 0)
        {
            return new PagedResult<NearbyFeedPostDto>(entry.Items, entry.TotalCount, request.Page, request.PageSize);
        }

        IReadOnlyList<Guid> postIds = entry.Items.Select(p => p.Id).ToList();
        Guid? viewerId = Guid.TryParse(_currentUserService?.UserId, out Guid parsedUserId) && parsedUserId != Guid.Empty
            ? parsedUserId
            : null;

        IReadOnlyDictionary<Guid, int> likeCounts = _likeRepository is not null
            ? await _likeRepository.CountByPostIdsAsync(postIds, cancellationToken)
            : new Dictionary<Guid, int>();

        IReadOnlyDictionary<Guid, int> commentCounts = _commentRepository is not null
            ? await _commentRepository.CountByPostIdsAsync(postIds, cancellationToken)
            : new Dictionary<Guid, int>();

        IReadOnlySet<Guid> likedIds = viewerId.HasValue && _likeRepository is not null
            ? await _likeRepository.GetLikedPostIdsByUserAsync(viewerId.Value, postIds, cancellationToken)
            : new HashSet<Guid>();

        IReadOnlyList<NearbyFeedPostDto> enriched = entry.Items
            .Select(item => item with
            {
                LikesCount = likeCounts.TryGetValue(item.Id, out int likes) ? likes : item.LikesCount,
                CommentsCount = commentCounts.TryGetValue(item.Id, out int comments) ? comments : item.CommentsCount,
                IsLikedByMe = likedIds.Contains(item.Id),
                IsMine = viewerId.HasValue && item.AuthorUserId == viewerId.Value
            })
            .ToList();

        return new PagedResult<NearbyFeedPostDto>(enriched, entry.TotalCount, request.Page, request.PageSize);
    }
}
