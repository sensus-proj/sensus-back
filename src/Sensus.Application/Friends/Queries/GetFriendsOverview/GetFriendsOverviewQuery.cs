using MediatR;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Friends.Queries.GetFriendsOverview;

public sealed record GetFriendsOverviewQuery : IRequest<Result<FriendsOverviewDto>>;
