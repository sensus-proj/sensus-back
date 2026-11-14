using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Queries.GetProfile;

public sealed record GetProfileQuery : IRequest<Result<UserDto>>;

public sealed class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService? _cacheService;

    public GetProfileQueryHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        ICacheService? cacheService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<UserDto>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var rawUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
        {
            return Result.Failure<UserDto>(Error.Unauthorized("Auth.Unauthorized", "Se requiere sesión activa."));
        }

        var cacheKey = $"sensus:users:{userId}:profile";
        if (_cacheService is not null)
        {
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result.Success(cached);
            }
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Estatus)
        {
            return Result.Failure<UserDto>(Error.NotFound("User.NotFound", "El usuario no existe o se encuentra inactivo."));
        }

        var dto = user.ToDto();
        if (_cacheService is not null)
        {
            await _cacheService.SetForEntityAsync(cacheKey, dto, "UserProfile", cancellationToken);
        }

        return Result.Success(dto);
    }
}
