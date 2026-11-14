using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.UploadMedia;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using IMediaAssetRepository = Sensus.Domain.Repositories.IMediaAssetRepository;

namespace Sensus.Application.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandHandler : IRequestHandler<UploadMediaCommand, Result<UploadMediaResponseDto>>
{
    private readonly IMediaStorage _mediaStorage;
    private readonly IImageModerationService _moderationService;
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UploadMediaCommandHandler(
        IMediaStorage mediaStorage,
        IImageModerationService moderationService,
        IMediaAssetRepository mediaAssetRepository,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _mediaStorage = mediaStorage;
        _moderationService = moderationService;
        _mediaAssetRepository = mediaAssetRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UploadMediaResponseDto>> Handle(
        UploadMediaCommand request,
        CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out Guid parsedUserId))
        {
            return Result.Failure<UploadMediaResponseDto>(
                Error.Unauthorized("Media.Unauthenticated", "Usuario no autenticado."));
        }

        // Verificar límite diario de 20 archivos por usuario (FR-022-07)
        DateTime todayUtc = _dateTimeProvider.UtcNow.Date;
        int uploadsToday = await _mediaAssetRepository.CountByUserAndDateAsync(parsedUserId, todayUtc, cancellationToken);
        if (uploadsToday >= 20)
        {
            return Result.Failure<UploadMediaResponseDto>(
                Error.Validation("Media.DailyLimitExceeded", "Se ha alcanzado el límite de 20 cargas por usuario y día."));
        }

        // Subir a Cloudinary de forma privada (hasta aprobación) (FR-022-04)
        Result<MediaUploadResult> uploadResult = await _mediaStorage.UploadImageAsync(
            request.ImageBytes,
            request.ContentType,
            parsedUserId,
            cancellationToken);

        if (uploadResult.IsFailure)
        {
            return Result.Failure<UploadMediaResponseDto>(uploadResult.Error);
        }

        // Moderar la imagen antes de exponerla públicamente (FR-022-06)
        ImageModerationResult moderationResult = await _moderationService.ModerateImageBytesAsync(
            request.ImageBytes,
            request.ContentType,
            request.TextContext,
            cancellationToken);

        MediaAssetStatus status = moderationResult.IsApproved
            ? MediaAssetStatus.Approved
            : moderationResult.Decision == "Quarantine"
                ? MediaAssetStatus.Quarantined
                : MediaAssetStatus.Rejected;

        MediaAsset mediaAsset = MediaAsset.Create(
            parsedUserId,
            uploadResult.Value.PublicId,
            uploadResult.Value.SecureUrl,
            request.ContentType,
            status,
            _dateTimeProvider.UtcNow);

        await _mediaAssetRepository.AddAsync(mediaAsset, cancellationToken);

        UploadMediaResponseDto dto = new UploadMediaResponseDto(
            MediaId: mediaAsset.Id,
            PublicId: mediaAsset.PublicId,
            SecureUrl: mediaAsset.SecureUrl,
            Status: mediaAsset.Status.ToString(),
            ContentType: mediaAsset.ContentType,
            ModerationDecision: moderationResult.Decision,
            ModerationMessage: moderationResult.Message ?? string.Empty,
            FlaggedCategories: moderationResult.FlaggedCategories,
            CreatedAtUtc: mediaAsset.CreatedAtUtc);

        return Result.Success(dto);
    }
}
