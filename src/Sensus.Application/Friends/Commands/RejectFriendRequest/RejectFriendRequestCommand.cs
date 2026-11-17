using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Commands.RejectFriendRequest;

public sealed record RejectFriendRequestCommand(Guid RequestId) : IRequest<Result>;
