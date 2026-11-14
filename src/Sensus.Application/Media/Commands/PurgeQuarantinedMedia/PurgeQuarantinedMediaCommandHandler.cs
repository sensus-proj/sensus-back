using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Media.Commands.PurgeQuarantinedMedia;

public sealed class PurgeQuarantinedMediaCommandHandler
    : IRequestHandler<PurgeQuarantinedMediaCommand, Result<PurgeQuarantinedMediaResultDto>>
{
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly IMediaStorage _mediaStorage;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<PurgeQuarantinedMediaCommandHandler> _logger;

    public PurgeQuarantinedMediaCommandHandler(
        IMediaAssetRepository mediaAssetRepository,
        IMediaStorage mediaStorage,
        IDateTimeProvider dateTimeProvider,
        ILogger<PurgeQuarantinedMediaCommandHandler> logger)
    {
        _mediaAssetRepository = mediaAssetRepository;
        _mediaStorage = mediaStorage;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<PurgeQuarantinedMediaResultDto>> Handle(
        PurgeQuarantinedMediaCommand request,
        CancellationToken cancellationToken)
    {
        if (request.RetentionDays <= 0)
        {
            return Result.Failure<PurgeQuarantinedMediaResultDto>(
                Error.Validation("Media.RetentionInvalid", "La retención de cuarentena debe ser de al menos un día."));
        }

        DateTime now = _dateTimeProvider.UtcNow;
        TimeSpan retention = TimeSpan.FromDays(request.RetentionDays);

        IReadOnlyList<MediaAsset> expired = await _mediaAssetRepository.GetQuarantinedCreatedBeforeAsync(
            now - retention,
            PurgeQuarantinedMediaCommand.BatchSize,
            cancellationToken);

        int purged = 0;
        int failed = 0;
        foreach (MediaAsset asset in expired)
        {
            if (!asset.IsQuarantineExpired(now, retention))
            {
                continue;
            }

            // Si el almacenamiento no confirma la eliminación se conserva el registro para reintentar en la siguiente ejecución.
            bool deleted = await _mediaStorage.DeleteImageAsync(asset.PublicId, cancellationToken);
            if (!deleted)
            {
                failed++;
                _logger.LogWarning("No se pudo eliminar el activo en cuarentena {MediaId} del almacenamiento.", asset.Id);
                continue;
            }

            asset.Purge();
            await _mediaAssetRepository.UpdateAsync(asset, cancellationToken);
            purged++;
        }

        _logger.LogInformation(
            "Purga de cuarentena: {Purged} activos descartados y {Failed} pendientes de reintento (retención {Days} días).",
            purged,
            failed,
            request.RetentionDays);

        return Result.Success(new PurgeQuarantinedMediaResultDto(purged, failed, now));
    }
}
