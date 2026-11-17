using MediatR;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Commands.ShareOfferWithFriends;

public sealed record ShareOfferWithFriendsCommand(
    Guid OfferId,
    IReadOnlyList<Guid> RecipientUserIds) : IRequest<Result<ShareOfferResultDto>>;
