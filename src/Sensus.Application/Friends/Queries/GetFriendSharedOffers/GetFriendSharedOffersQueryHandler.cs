using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Queries.GetFriendSharedOffers;

public sealed class GetFriendSharedOffersQueryHandler
    : IRequestHandler<GetFriendSharedOffersQuery, Result<FriendSharedOffersThreadDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly ISharedOfferRepository _sharedOfferRepository;

    public GetFriendSharedOffersQueryHandler(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        IFriendRequestRepository friendRequestRepository,
        ISharedOfferRepository sharedOfferRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _friendRequestRepository = friendRequestRepository;
        _sharedOfferRepository = sharedOfferRepository;
    }

    public async Task<Result<FriendSharedOffersThreadDto>> Handle(
        GetFriendSharedOffersQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out Guid userId))
        {
            return Result.Failure<FriendSharedOffersThreadDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para ver las ofertas compartidas."));
        }

        if (request.FriendUserId == Guid.Empty)
        {
            return Result.Failure<FriendSharedOffersThreadDto>(
                Error.Validation("Friends.InvalidFriendId", "El identificador del amigo no es válido."));
        }

        FriendRequest? relation = await _friendRequestRepository.GetBetweenUsersAsync(userId, request.FriendUserId, cancellationToken);
        if (relation is null || relation.Status != FriendRequestStatus.Accepted)
        {
            return Result.Failure<FriendSharedOffersThreadDto>(
                Error.Forbidden("Friends.NotFriends", "Solo puedes ver ofertas compartidas con amigos conectados."));
        }

        User? friendUser = await _userRepository.GetByIdAsync(request.FriendUserId, cancellationToken);
        FriendItemDto friendDto = relation.ToFriendDtoFor(userId, friendUser?.FotoPerfilUrl);
        if (friendUser is not null)
        {
            friendDto = friendDto with
            {
                DisplayName = friendUser.NombreCompleto,
                PhotoUrl = friendUser.FotoPerfilUrl
            };
        }

        IReadOnlyList<SharedOffer> offers = await _sharedOfferRepository.GetConversationAsync(
            userId,
            request.FriendUserId,
            cancellationToken);

        return Result.Success(new FriendSharedOffersThreadDto(
            Friend: friendDto,
            Offers: offers.OrderBy(o => o.SharedAtUtc).Select(o => o.ToDto()).ToList()));
    }
}
