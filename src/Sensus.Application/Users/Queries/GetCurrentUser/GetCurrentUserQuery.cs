using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Queries.GetCurrentUser;

/// <summary>
/// Consulta de solo lectura para proyectar la identidad de la sesión desde los claims del JWT (FR-002-06, FR-002-07).
/// </summary>
public sealed record GetCurrentUserQuery : IRequest<Result<CurrentUserDto>>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentUserQueryHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure<CurrentUserDto>(
                Error.Unauthorized("Auth.Unauthorized", "La sesión actual no está autenticada.")));
        }

        IReadOnlyList<string> roles = _currentUserService.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dto = new CurrentUserDto(
            UserId: _currentUserService.UserId,
            NombreCompleto: _currentUserService.FullName,
            Correo: _currentUserService.Email,
            Roles: roles);

        return Task.FromResult(Result.Success(dto));
    }
}
