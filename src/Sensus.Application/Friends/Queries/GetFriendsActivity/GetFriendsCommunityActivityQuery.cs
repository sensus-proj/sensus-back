using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Queries.GetFriendsActivity;

/// <summary>
/// Obtiene de forma paginada las reseñas, respuestas y me gusta realizados por tus amigos
/// conectados y por el usuario actual (Spec 058 / FR-058-04).
/// </summary>
public sealed record GetFriendsCommunityActivityQuery(
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize)
    : IRequest<Result<PagedResult<CommunityActivityItemDto>>>;

public interface ICommunityActivityReader
{
    Task<PagedResult<CommunityActivityItemDto>> ReadAsync(
        IReadOnlyCollection<Guid> visibleActorIds, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class GetFriendsCommunityActivityQueryValidator : AbstractValidator<GetFriendsCommunityActivityQuery>
{
    public GetFriendsCommunityActivityQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PaginationPolicy.MaxPageSize);
    }
}

public sealed class GetFriendsCommunityActivityQueryHandler(
    ICurrentUserService currentUser,
    IFriendRequestRepository friends,
    ICommunityActivityReader activity)
    : IRequestHandler<GetFriendsCommunityActivityQuery, Result<PagedResult<CommunityActivityItemDto>>>
{
    public const string ActivityOfferLike = "OfferLike";
    public const string ActivityStoreReview = "StoreReview";
    public const string ActivityReviewReply = "ReviewReply";

    public async Task<Result<PagedResult<CommunityActivityItemDto>>> Handle(
        GetFriendsCommunityActivityQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetUserId(out Guid userId) || userId == Guid.Empty)
        {
            return Result.Failure<PagedResult<CommunityActivityItemDto>>(
                Error.Unauthorized("Community.Unauthorized", "Debes iniciar sesión para ver la actividad de tus amigos."));
        }

        IReadOnlyList<FriendRequest> friendships =
            await friends.GetForUserAsync(userId, cancellationToken) ?? [];
        HashSet<Guid> visibleActorIds = friendships
            .Where(friend => friend.Status == FriendRequestStatus.Accepted
                && (friend.SenderUserId == userId || friend.TargetUserId == userId))
            .Select(friend => friend.CounterpartIdFor(userId))
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        // FR-058-02: Incluye también la actividad del usuario actual para que sus amigos la vean y él vea su historial reciente.
        visibleActorIds.Add(userId);

        return Result.Success(await activity.ReadAsync(visibleActorIds, request.Page, request.PageSize, cancellationToken));
    }
}
