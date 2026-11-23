using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.UpdateProfile;

/// <summary>
/// Actualiza el perfil de un usuario con moderación auditada fail-closed (Spec 021, B3)
/// e invalidación de caché CDN ante cambio de foto (Spec 023, B2).
/// </summary>
public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator;
    private readonly IProfileImageStorage? _imageStorage;
    private readonly ICacheService? _cacheService;
    private readonly ITextModerationService? _textModerationService;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IModerationAuditRecorder auditRecorder,
        ICdnCacheInvalidator cdnCacheInvalidator,
        IProfileImageStorage? imageStorage = null,
        ICacheService? cacheService = null,
        ITextModerationService? textModerationService = null)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _auditRecorder = auditRecorder;
        _cdnCacheInvalidator = cdnCacheInvalidator;
        _imageStorage = imageStorage;
        _cacheService = cacheService;
        _textModerationService = textModerationService;
    }

    public async Task<Result<UserDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var rawUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
        {
            return Result.Failure<UserDto>(Error.Unauthorized("Auth.Unauthorized", "Se requiere sesión activa para editar el perfil."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Estatus)
        {
            return Result.Failure<UserDto>(Error.NotFound("User.NotFound", "El usuario no existe o se encuentra inactivo."));
        }

        ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
            _textModerationService,
            request.NombreCompleto,
            "Actualización de nombre de usuario",
            "User.NameModerationRejected",
            "El nombre de usuario contiene lenguaje antisonante u ofensivo.",
            cancellationToken);

        var auditEntry = new ModerationAuditEntry(
            user.Id,
            ModerationTargetType.User,
            evaluation.Verdict,
            evaluation.Decision,
            "user_update_profile",
            evaluation.FlaggedCategories,
            evaluation.CategoryScores,
            ActorId: userId.ToString(),
            ActorType: ModerationActorType.User);

        if (!evaluation.IsApproved)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);
            return Result.Failure<UserDto>(evaluation.RejectionError ?? Error.Validation("User.ModerationRejected", "Contenido rechazado por moderación."));
        }

        Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
        if (auditResult.IsFailure)
        {
            return Result.Failure<UserDto>(auditResult.Error);
        }

        var previousPhotoUrl = user.FotoPerfilUrl;

        var updateResult = user.UpdateProfile(request.NombreCompleto, request.FotoPerfilUrl);
        if (updateResult.IsFailure)
        {
            return Result.Failure<UserDto>(updateResult.Error);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        // Invalidar caché del perfil de usuario (Spec 048)
        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync($"sensus:users:{userId}:profile", cancellationToken);
        }

        // Limpiar la foto anterior en Cloudinary para no acumular imágenes huérfanas en plan Free e invalidar en CDN (CA-023-03)
        if (_imageStorage is not null &&
            !string.IsNullOrWhiteSpace(previousPhotoUrl) &&
            !string.Equals(previousPhotoUrl, user.FotoPerfilUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _imageStorage.DeleteImageAsync(previousPhotoUrl, cancellationToken);
            await _cdnCacheInvalidator.InvalidateAsync(previousPhotoUrl, cancellationToken);
        }

        return Result.Success(user.ToDto());
    }
}
