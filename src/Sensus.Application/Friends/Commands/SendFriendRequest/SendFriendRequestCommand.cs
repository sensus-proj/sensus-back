using MediatR;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Commands.SendFriendRequest;

public sealed record SendFriendRequestCommand(
    string InviteCode,
    string? AddedVia = "QR") : IRequest<Result<SendFriendRequestResponseDto>>;
