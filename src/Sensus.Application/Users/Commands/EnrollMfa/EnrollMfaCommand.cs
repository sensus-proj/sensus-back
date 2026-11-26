using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.EnrollMfa;

/// <summary>
/// Inicia el enrolamiento TOTP RFC 6238 para cuentas con rol Administrador (Spec 062, FR-062-03).
/// </summary>
public sealed record EnrollMfaCommand : IRequest<Result<EnrollMfaResponseDto>>, ISensitiveRequest;

public sealed record EnrollMfaResponseDto(
    string Secret,
    string OtpauthUri,
    IReadOnlyList<string> RecoveryCodes);

public sealed class EnrollMfaCommandHandler : IRequestHandler<EnrollMfaCommand, Result<EnrollMfaResponseDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly MfaSettings _mfaSettings;

    public EnrollMfaCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ITotpService totpService,
        IModerationAuditRecorder auditRecorder,
        IOptions<MfaSettings>? mfaSettings = null)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _totpService = totpService;
        _auditRecorder = auditRecorder;
        _mfaSettings = mfaSettings?.Value ?? new MfaSettings();
    }

    public async Task<Result<EnrollMfaResponseDto>> Handle(
        EnrollMfaCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<EnrollMfaResponseDto>(
                Error.Unauthorized("Auth.Unauthorized", "Sesión inválida o no autenticada."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted || !user.Estatus)
        {
            return Result.Failure<EnrollMfaResponseDto>(
                Error.Unauthorized("Auth.Unauthorized", "La cuenta no existe o se encuentra inactiva."));
        }

        if (!user.HasRole(RoleType.Administrador))
        {
            return Result.Failure<EnrollMfaResponseDto>(
                Error.Forbidden("Auth.AdminRoleRequired", "El segundo factor administrativo es exclusivo del rol Administrador."));
        }

        if (user.Mfa is { IsEnabled: true })
        {
            return Result.Failure<EnrollMfaResponseDto>(
                Error.Conflict("Auth.MfaAlreadyEnabled", "El segundo factor ya está activo para esta cuenta."));
        }

        string plainSecret = _totpService.GenerateSecret();
        Result<string> encryptedSecret = _totpService.EncryptSecret(plainSecret);
        if (encryptedSecret.IsFailure)
        {
            return Result.Failure<EnrollMfaResponseDto>(encryptedSecret.Error);
        }

        IReadOnlyList<string> recoveryCodes = _totpService.GenerateRecoveryCodes(8);
        List<string> recoveryHashes = recoveryCodes.Select(_totpService.HashRecoveryCode).ToList();

        Result enrollResult = user.BeginMfaEnrollment(encryptedSecret.Value, recoveryHashes);
        if (enrollResult.IsFailure)
        {
            return Result.Failure<EnrollMfaResponseDto>(enrollResult.Error);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        await _auditRecorder.RecordBestEffortAsync(
            new ModerationAuditEntry(
                TargetId: user.Id,
                TargetType: ModerationTargetType.AdminMfa,
                Verdict: ModerationVerdict.Approved,
                Decision: "MfaEnrolledPending",
                ContentContext: "admin:mfa:enroll",
                ActorId: user.Id.ToString(),
                ActorType: ModerationActorType.Administrator),
            cancellationToken);

        string otpauthUri = _totpService.BuildOtpAuthUri(_mfaSettings.Issuer, user.Correo, plainSecret);

        return Result.Success(new EnrollMfaResponseDto(plainSecret, otpauthUri, recoveryCodes));
    }
}
