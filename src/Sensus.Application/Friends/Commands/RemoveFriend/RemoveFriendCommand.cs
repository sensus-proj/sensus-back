using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Commands.RemoveFriend;

public sealed record RemoveFriendCommand(Guid FriendUserId) : IRequest<Result>;
