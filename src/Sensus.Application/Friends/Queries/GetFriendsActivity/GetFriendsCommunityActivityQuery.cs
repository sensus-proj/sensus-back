using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Queries.GetFriendsActivity;

/// <summary>
/// Obtiene exclusivamente los comentarios (reseñas/respuestas) y me gusta realizados por tus amigos
/// conectados y por el usuario actual (para compartir actividad entre amigos en Comunidad).
/// </summary>
public sealed record GetFriendsCommunityActivityQuery(int Limit = 50)
    : IRequest<Result<IReadOnlyList<CommunityActivityItemDto>>>;

public sealed class GetFriendsCommunityActivityQueryHandler
    : IRequestHandler<GetFriendsCommunityActivityQuery, Result<IReadOnlyList<CommunityActivityItemDto>>>
{
    public const string ActivityOfferLike = "OfferLike";
    public const string ActivityStoreReview = "StoreReview";
    public const string ActivityReviewReply = "ReviewReply";

    private readonly ICurrentUserService _currentUser;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly IOfferLikeRepository _offerLikeRepository;
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IUserRepository _userRepository;

    public GetFriendsCommunityActivityQueryHandler(
        ICurrentUserService currentUser,
        IFriendRequestRepository friendRequestRepository,
        IOfferLikeRepository offerLikeRepository,
        IStoreReviewRepository storeReviewRepository,
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IUserRepository userRepository)
    {
        _currentUser = currentUser;
        _friendRequestRepository = friendRequestRepository;
        _offerLikeRepository = offerLikeRepository;
        _storeReviewRepository = storeReviewRepository;
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<CommunityActivityItemDto>>> Handle(
        GetFriendsCommunityActivityQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out Guid userId))
        {
            return Result.Failure<IReadOnlyList<CommunityActivityItemDto>>(
                Error.Unauthorized("Community.Unauthorized", "Debes iniciar sesión para ver la actividad de tus amigos."));
        }

        int effectiveLimit = Math.Clamp(request.Limit, 1, 100);

        IReadOnlyList<FriendRequest> friendships =
            await _friendRequestRepository.GetForUserAsync(userId, cancellationToken) ?? [];
        HashSet<Guid> visibleActorIds = friendships
            .Where(f => f.Status == FriendRequestStatus.Accepted)
            .Select(f => f.CounterpartIdFor(userId))
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        // FR-056-02: Incluye también la actividad del usuario actual para que sus amigos la vean y él vea su historial reciente.
        visibleActorIds.Add(userId);

        IReadOnlyList<OfferLike> likes = await _offerLikeRepository.GetByUserIdsAsync(
            visibleActorIds,
            effectiveLimit,
            cancellationToken) ?? [];

        IReadOnlyList<StoreReview> reviews = await _storeReviewRepository.GetRecentActivityByUserIdsAsync(
            visibleActorIds,
            effectiveLimit,
            cancellationToken) ?? [];

        Dictionary<Guid, Offer?> offersById = await LoadOffersAsync(likes, cancellationToken);
        Dictionary<Guid, Comercio?> comerciosById = await LoadComerciosAsync(offersById.Values, reviews, cancellationToken);
        Dictionary<Guid, User?> usersById = await LoadUsersAsync(visibleActorIds, cancellationToken);

        var items = new List<CommunityActivityItemDto>();

        AppendOfferLikes(items, likes, offersById, comerciosById, usersById);
        AppendReviewAndReplyActivity(items, reviews, visibleActorIds, comerciosById, usersById);

        List<CommunityActivityItemDto> ordered = items
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(effectiveLimit)
            .ToList();

        return Result.Success<IReadOnlyList<CommunityActivityItemDto>>(ordered);
    }

    private async Task<Dictionary<Guid, Offer?>> LoadOffersAsync(
        IReadOnlyList<OfferLike> likes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Offer?>();
        foreach (Guid offerId in likes.Select(l => l.OfferId).Distinct())
        {
            result[offerId] = await _offerRepository.GetByIdAsync(offerId, cancellationToken);
        }

        return result;
    }

    private async Task<Dictionary<Guid, Comercio?>> LoadComerciosAsync(
        IEnumerable<Offer?> offers,
        IReadOnlyList<StoreReview> reviews,
        CancellationToken cancellationToken)
    {
        var merchantIds = offers
            .Where(o => o is not null)
            .Select(o => o!.MerchantId)
            .Concat(reviews.Select(r => r.MerchantId))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var result = new Dictionary<Guid, Comercio?>();
        foreach (Guid merchantId in merchantIds)
        {
            result[merchantId] = await _comercioRepository.GetByIdAsync(merchantId, cancellationToken);
        }

        return result;
    }

    private async Task<Dictionary<Guid, User?>> LoadUsersAsync(
        IEnumerable<Guid> actorIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, User?>();
        foreach (Guid actorId in actorIds.Distinct())
        {
            result[actorId] = await _userRepository.GetByIdAsync(actorId, cancellationToken);
        }

        return result;
    }

    private static void AppendOfferLikes(
        List<CommunityActivityItemDto> items,
        IReadOnlyList<OfferLike> likes,
        Dictionary<Guid, Offer?> offersById,
        Dictionary<Guid, Comercio?> comerciosById,
        Dictionary<Guid, User?> usersById)
    {
        foreach (OfferLike like in likes)
        {
            Offer? offer = offersById.GetValueOrDefault(like.OfferId);
            if (offer is null || offer.Status == OfferStatus.Archived)
            {
                continue;
            }

            User? actor = usersById.GetValueOrDefault(like.UserId);
            Comercio? merchant = comerciosById.GetValueOrDefault(offer.MerchantId);
            items.Add(new CommunityActivityItemDto(
                Id: $"offer-like:{like.Id}",
                ActivityType: ActivityOfferLike,
                ActorUserId: like.UserId,
                ActorName: actor?.NombreCompleto ?? "Amigo Sensus",
                ActorPhotoUrl: actor?.FotoPerfilUrl,
                OfferId: offer.Id,
                MerchantId: offer.MerchantId,
                TargetTitle: offer.Title,
                TargetSubtitle: merchant?.NombreComercial,
                CommentText: null,
                Rating: null,
                CreatedAtUtc: like.CreatedAtUtc));
        }
    }

    private static void AppendReviewAndReplyActivity(
        List<CommunityActivityItemDto> items,
        IReadOnlyList<StoreReview> reviews,
        HashSet<Guid> visibleActorIds,
        Dictionary<Guid, Comercio?> comerciosById,
        Dictionary<Guid, User?> usersById)
    {
        foreach (StoreReview review in reviews)
        {
            Comercio? merchant = comerciosById.GetValueOrDefault(review.MerchantId);
            string merchantName = merchant?.NombreComercial ?? "Comercio local";

            if (visibleActorIds.Contains(review.AuthorUserId))
            {
                User? author = usersById.GetValueOrDefault(review.AuthorUserId);
                items.Add(new CommunityActivityItemDto(
                    Id: $"review:{review.Id}",
                    ActivityType: ActivityStoreReview,
                    ActorUserId: review.AuthorUserId,
                    ActorName: author?.NombreCompleto ?? review.AuthorName,
                    ActorPhotoUrl: author?.FotoPerfilUrl ?? review.AuthorPhotoUrl,
                    OfferId: null,
                    MerchantId: review.MerchantId,
                    TargetTitle: merchantName,
                    TargetSubtitle: merchant?.Direccion,
                    CommentText: review.Comment,
                    Rating: review.Rating,
                    CreatedAtUtc: review.CreatedAtUtc));
            }

            foreach (StoreReviewReply reply in review.Replies)
            {
                if (!visibleActorIds.Contains(reply.AuthorUserId))
                {
                    continue;
                }

                User? replyAuthor = usersById.GetValueOrDefault(reply.AuthorUserId);
                items.Add(new CommunityActivityItemDto(
                    Id: $"reply:{reply.Id}",
                    ActivityType: ActivityReviewReply,
                    ActorUserId: reply.AuthorUserId,
                    ActorName: replyAuthor?.NombreCompleto ?? reply.AuthorName,
                    ActorPhotoUrl: replyAuthor?.FotoPerfilUrl ?? reply.AuthorPhotoUrl,
                    OfferId: null,
                    MerchantId: review.MerchantId,
                    TargetTitle: merchantName,
                    TargetSubtitle: $"Respondió a {review.AuthorName}",
                    CommentText: reply.Comment,
                    Rating: null,
                    CreatedAtUtc: reply.CreatedAtUtc));
            }
        }
    }
}
