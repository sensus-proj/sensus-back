using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Queries.GetFriendsActivity;

public sealed record GetFriendsCommunityActivityQuery(int Page = 1, int PageSize = 20)
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
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetFriendsCommunityActivityQueryHandler(
    ICurrentUserService currentUser,
    IFriendRequestRepository friends,
    ICommunityActivityReader activity)
    : IRequestHandler<GetFriendsCommunityActivityQuery, Result<PagedResult<CommunityActivityItemDto>>>
{
    public async Task<Result<PagedResult<CommunityActivityItemDto>>> Handle(
        GetFriendsCommunityActivityQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetUserId(out var userId) || userId == Guid.Empty)
        {
            return Result.Failure<PagedResult<CommunityActivityItemDto>>(
                Error.Unauthorized("Community.Unauthorized", "Debes iniciar sesión para ver la actividad de tus amigos."));
        }

        var friendships = await friends.GetForUserAsync(userId, cancellationToken);
        var visibleActorIds = friendships
            .Where(friend => friend.Status == FriendRequestStatus.Accepted
                && (friend.SenderUserId == userId || friend.TargetUserId == userId))
            .Select(friend => friend.CounterpartIdFor(userId))
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        visibleActorIds.Add(userId);

        return Result.Success(await activity.ReadAsync(visibleActorIds, request.Page, request.PageSize, cancellationToken));
    }
}
