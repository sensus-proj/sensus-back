using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Users.Common;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.VerifyMfa;

/// <summary>
/// Verifica código TOTP o código de recuperación para Administrador y emite JWT step-up (amr=mfa, mfa_at) (Spec 062).
/// </summary>
public sealed record VerifyMfaCommand(
    string? Code = null,
    string? RecoveryCode = null) : IRequest<Result<AuthResponseDto>>, ISensitiveRequest;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithMessage("Debe proporcionar un código TOTP o un código de recuperación.");
    }
}

public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, Result<AuthResponseDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly IRefreshTokenRepository? _refreshTokenRepository;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly MfaSettings _mfaSettings;

    public VerifyMfaCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ITotpService totpService,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IModerationAuditRecorder auditRecorder,
        IRefreshTokenRepository? refreshTokenRepository = null,
        IOptions<RefreshTokenSettings>? refreshTokenSettings = null,
        IOptions<MfaSettings>? mfaSettings = null)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _totpService = totpService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _auditRecorder = auditRecorder;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenSettings = refreshTokenSettings?.Value ?? new RefreshTokenSettings();
        _mfaSettings = mfaSettings?.Value ?? new MfaSettings();
    }

    public async Task<Result<AuthResponseDto>> Handle(
        VerifyMfaCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<AuthResponseDto>(
                Error.Unauthorized("Auth.Unauthorized", "Sesión inválida o no autenticada."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted || !user.Estatus)
        {
            return Result.Failure<AuthResponseDto>(
                Error.Unauthorized("Auth.Unauthorized", "La cuenta no existe o se encuentra inactiva."));
        }

        if (!user.HasRole(RoleType.Administrador))
        {
            return Result.Failure<AuthResponseDto>(
                Error.Forbidden("Auth.AdminRoleRequired", "La verificación MFA administrativa es exclusiva del rol Administrador."));
        }

        var now = _dateTimeProvider.UtcNow;

        if (user.Mfa is null)
        {
            return Result.Failure<AuthResponseDto>(
                Error.Unauthorized("Auth.MfaNotEnrolled", "El segundo factor no está configurado."));
        }

        if (user.IsMfaLockedOut(now))
        {
            await RecordAuditAsync(user.Id, ModerationVerdict.Rejected, "MfaLockedAttempt", cancellationToken);
            return Result.Failure<AuthResponseDto>(
                Error.Forbidden("Auth.MfaLocked", "Demasiados intentos fallidos de verificación MFA. Intenta más tarde."));
        }

        long? matchedTimeStep = null;
        string? consumedRecoveryHash = null;
        bool verified = false;

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            Result<string> decryptedSecret = _totpService.DecryptSecret(user.Mfa.EncryptedSecret);
            if (decryptedSecret.IsFailure)
            {
                return Result.Failure<AuthResponseDto>(decryptedSecret.Error);
            }

            if (_totpService.TryVerifyCode(decryptedSecret.Value, request.Code, now, out long step, allowedStepDrift: 1) &&
                !user.IsTotpTimeStepReplayed(step))
            {
                matchedTimeStep = step;
                verified = true;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            if (_totpService.VerifyRecoveryCode(request.RecoveryCode, user.Mfa.RecoveryCodeHashes, out string? hash))
            {
                consumedRecoveryHash = hash;
                verified = true;
            }
        }

        if (!verified)
        {
            bool locked = user.RecordMfaFailure(now, _mfaSettings.MaxFailedAttempts, _mfaSettings.LockoutMinutes);
            await _userRepository.UpdateAsync(user, cancellationToken);
            await RecordAuditAsync(
                user.Id,
                ModerationVerdict.Rejected,
                locked ? "MfaLockedOut" : "MfaVerifyFailed",
                cancellationToken);

            return Result.Failure<AuthResponseDto>(locked
                ? Error.Forbidden("Auth.MfaLocked", "Demasiados intentos fallidos de verificación MFA. Intenta más tarde.")
                : Error.Unauthorized("Auth.InvalidMfaCode", "El código de verificación MFA es inválido."));
        }

        bool wasPending = user.Mfa.IsPendingConfirmation;
        Result completeResult = user.CompleteMfaVerification(matchedTimeStep, now, consumedRecoveryHash);
        if (completeResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(completeResult.Error);
        }

        user.RecordAccess(now);
        await _userRepository.UpdateAsync(user, cancellationToken);

        await RecordAuditAsync(
            user.Id,
            ModerationVerdict.Approved,
            wasPending ? "MfaActivated" : "MfaStepUpVerified",
            cancellationToken);

        var (token, expiresAt) = _jwtTokenGenerator.GenerateAdminStepUpToken(user, now);
        var (refreshToken, refreshExpiresAt, _) = await RefreshTokenIssuer.IssueAsync(
            _refreshTokenRepository,
            user.Id,
            now,
            _refreshTokenSettings.RefreshTokenDays,
            cancellationToken: cancellationToken);

        return Result.Success(new AuthResponseDto(
            token,
            "Bearer",
            expiresAt,
            user.ToDto(),
            refreshToken,
            refreshExpiresAt));
    }

    private Task RecordAuditAsync(Guid adminId, ModerationVerdict verdict, string decision, CancellationToken cancellationToken) =>
        _auditRecorder.RecordBestEffortAsync(
            new ModerationAuditEntry(
                TargetId: adminId,
                TargetType: ModerationTargetType.AdminMfa,
                Verdict: verdict,
                Decision: decision,
                ContentContext: "admin:mfa:verify",
                ActorId: adminId.ToString(),
                ActorType: ModerationActorType.Administrator),
            cancellationToken);
}
