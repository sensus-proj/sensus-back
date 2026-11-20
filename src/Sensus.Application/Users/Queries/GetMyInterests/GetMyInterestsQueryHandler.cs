using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Interests;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Queries.GetMyInterests;

public sealed class GetMyInterestsQueryHandler : IRequestHandler<GetMyInterestsQuery, Result<UserInterestsDto>>
{
    private readonly IUserInterestsRepository _userInterestsRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyInterestsQueryHandler(
        IUserInterestsRepository userInterestsRepository,
        ICurrentUserService currentUserService)
    {
        _userInterestsRepository = userInterestsRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserInterestsDto>> Handle(GetMyInterestsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest)
        {
            return Result.Failure<UserInterestsDto>(
                Error.Forbidden("Interests.GuestForbidden", "Los usuarios invitados no tienen intereses personales."));
        }

        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId) ||
            userId == Guid.Empty)
        {
            return Result.Failure<UserInterestsDto>(
                Error.Unauthorized("Interests.Unauthorized", "Se requiere autenticación para consultar intereses."));
        }

        UserInterests? interests = await _userInterestsRepository.GetByUserIdAsync(userId, cancellationToken);
        return Result.Success(interests.ToDto());
    }
}
