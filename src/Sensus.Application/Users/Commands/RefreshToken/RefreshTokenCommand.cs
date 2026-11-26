using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Sanctions.Common;
using Sensus.Application.Users.Common;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.Application.Users.Commands.RefreshToken;

/// <summary>
/// Rota un refresh token válido y emite un nuevo JWT de corta vigencia (G-06 / Spec 002).
/// Ante reutilización de un token ya rotado o revocado, revoca toda la familia y rota el SecurityStamp.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<AuthResponseDto>>, ISensitiveRequest;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El refresh token es obligatorio.")
            .MaximumLength(512).WithMessage("El refresh token excede el tamaño máximo permitido.");
    }
}

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly IOptions<RefreshTokenSettings>? _refreshSettings;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        ILogger<RefreshTokenCommandHandler> logger,
        IOptions<RefreshTokenSettings>? refreshSettings = null)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _refreshSettings = refreshSettings;
    }

    public static string HashToken(string rawToken) => RefreshTokenIssuer.HashToken(rawToken);

    public async Task<Result<AuthResponseDto>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        Error invalidError = Error.Unauthorized(
            "Auth.InvalidRefreshToken",
            "El token de actualización es inválido o ha expirado.");

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        DateTime now = _dateTimeProvider.UtcNow;
        string tokenHash = HashToken(request.RefreshToken);
        RefreshTokenEntity? existing = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existing is null)
        {
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        // Detección de reutilización (replay): si ya fue usado o revocado, comprometemos toda la familia.
        if (existing.WasUsedOrRevoked)
        {
            await RevokeCompromisedSessionAsync(existing.UserId, existing.FamilyId, now, "reuse_detected", cancellationToken);
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        if (existing.ExpiresAtUtc <= now)
        {
            await _refreshTokenRepository.RevokeFamilyAsync(existing.FamilyId, now, "expired", cancellationToken);
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        User? user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || user.IsDeleted || !user.Estatus || user.IsSuspended(now))
        {
            await _refreshTokenRepository.RevokeFamilyAsync(existing.FamilyId, now, "user_unavailable", cancellationToken);
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        int refreshDays = _refreshSettings?.Value.RefreshTokenDays ?? RefreshTokenSettings.DefaultRefreshTokenDays;
        string newRawToken = RefreshTokenIssuer.GenerateRawToken();
        string newHash = HashToken(newRawToken);
        DateTime newRefreshExpiry = DateTime.SpecifyKind(now, DateTimeKind.Utc).AddDays(Math.Clamp(refreshDays, 1, 90));
        Guid newTokenId = Guid.NewGuid();

        Result<RefreshTokenEntity> nextResult = RefreshTokenEntity.Create(
            user.Id,
            newHash,
            now,
            newRefreshExpiry,
            existing.FamilyId,
            newTokenId);
        if (nextResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(nextResult.Error);
        }

        bool markedUsed = await _refreshTokenRepository.TryMarkAsUsedAsync(existing.Id, newTokenId, now, cancellationToken);
        if (!markedUsed)
        {
            await RevokeCompromisedSessionAsync(existing.UserId, existing.FamilyId, now, "concurrent_reuse_detected", cancellationToken);
            return Result.Failure<AuthResponseDto>(invalidError);
        }

        await _refreshTokenRepository.AddAsync(nextResult.Value, cancellationToken);

        (string accessToken, DateTime accessExpiresAt) = _jwtTokenGenerator.GenerateToken(user);

        return Result.Success(new AuthResponseDto(
            accessToken,
            "Bearer",
            accessExpiresAt,
            user.ToDto(),
            newRawToken,
            newRefreshExpiry));
    }

    private async Task RevokeCompromisedSessionAsync(
        Guid userId,
        Guid familyId,
        DateTime now,
        string reason,
        CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeFamilyAsync(familyId, now, reason, cancellationToken);

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is not null)
        {
            user.RotateSecurityStamp();
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        _logger.LogWarning(
            "Reutilización de refresh token detectada para usuario {UserId}, familia {FamilyId} ({Reason}). Sesión revocada.",
            userId,
            familyId,
            reason);
    }
}
