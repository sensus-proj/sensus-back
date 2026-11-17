using MediatR;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Queries.GetFriendSharedOffers;

public sealed record GetFriendSharedOffersQuery(Guid FriendUserId) : IRequest<Result<FriendSharedOffersThreadDto>>;
