using MediatR;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Commands.AcceptFriendRequest;

public sealed record AcceptFriendRequestCommand(Guid RequestId) : IRequest<Result<FriendItemDto>>;
