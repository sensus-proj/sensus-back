using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Sanctions.Common;
using Sensus.Application.Users.Common;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRefreshTokenRepository? _refreshTokenRepository;
    private readonly IOptions<RefreshTokenSettings>? _refreshTokenSettings;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IRefreshTokenRepository? refreshTokenRepository = null,
        IOptions<RefreshTokenSettings>? refreshTokenSettings = null)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenSettings = refreshTokenSettings;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // Mensaje genérico de seguridad para no exponer si el correo existe o no
        var invalidCredentialsError = Error.Unauthorized(
            "Auth.InvalidCredentials",
            "Correo o contraseña incorrectos.");

        var user = await _userRepository.GetByEmailAsync(request.Correo, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponseDto>(invalidCredentialsError);
        }

        // Spec 060 (FR-060-03): la cuenta dada de baja no puede iniciar sesión y se responde con el
        // mismo error genérico que para un correo inexistente, sin revelar su existencia.
        if (user.IsDeleted)
        {
            return Result.Failure<AuthResponseDto>(invalidCredentialsError);
        }

        if (!user.Estatus)
        {
            return Result.Failure<AuthResponseDto>(
                Error.Forbidden("Auth.UserDeactivated", "La cuenta se encuentra inactiva o suspendida."));
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure<AuthResponseDto>(invalidCredentialsError);
        }

        var now = _dateTimeProvider.UtcNow;

        // Spec 020 (FR-020-04, CA-020-03): se verifica DESPUÉS de la contraseña para no revelar a un atacante
        // que la cuenta existe ni que está sancionada (ASVS V2, no enumeración de usuarios).
        if (user.IsSuspended(now))
        {
            return Result.Failure<AuthResponseDto>(user.ToLoginError());
        }

        // Spec 062 (FR-062-04): cuentas Administrador con TOTP activo reciben un desafío intermedio
        // sin permisos operativos hasta completar /api/v1/auth/mfa/verify.
        if (user.HasRole(RoleType.Administrador) && user.Mfa is { IsEnabled: true })
        {
            var (challengeToken, challengeExpiresAt) = _jwtTokenGenerator.GenerateMfaChallengeToken(user);
            return Result.Success(new AuthResponseDto(
                string.Empty,
                "Bearer",
                challengeExpiresAt,
                user.ToDto(),
                RequiresMfa: true,
                MfaChallengeToken: challengeToken));
        }

        // Registrar último acceso
        user.RecordAccess(now);
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Generar token JWT con claims y roles + refresh token rotatorio (G-06)
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);
        int refreshDays = _refreshTokenSettings?.Value.RefreshTokenDays ?? RefreshTokenSettings.DefaultRefreshTokenDays;
        var (refreshToken, refreshExpiresAt, _) = await RefreshTokenIssuer.IssueAsync(
            _refreshTokenRepository,
            user.Id,
            now,
            refreshDays,
            cancellationToken: cancellationToken);

        var response = new AuthResponseDto(
            token,
            "Bearer",
            expiresAt,
            user.ToDto(),
            refreshToken,
            refreshExpiresAt);

        return Result.Success(response);
    }
}
