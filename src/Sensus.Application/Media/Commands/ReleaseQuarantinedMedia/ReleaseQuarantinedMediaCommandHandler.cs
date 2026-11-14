using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;

public sealed class ReleaseQuarantinedMediaCommandHandler
    : IRequestHandler<ReleaseQuarantinedMediaCommand, Result<ReleasedMediaDto>>
{
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ReleaseQuarantinedMediaCommandHandler> _logger;

    public ReleaseQuarantinedMediaCommandHandler(
        IMediaAssetRepository mediaAssetRepository,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<ReleaseQuarantinedMediaCommandHandler> logger)
    {
        _mediaAssetRepository = mediaAssetRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ReleasedMediaDto>> Handle(
        ReleaseQuarantinedMediaCommand request,
        CancellationToken cancellationToken)
    {
        if (request.MediaId == Guid.Empty)
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.Validation("Media.IdInvalid", "El identificador del activo es obligatorio."));
        }

        MediaAsset? asset = await _mediaAssetRepository.GetByIdAsync(request.MediaId, cancellationToken);
        if (asset is null)
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.NotFound("Media.NotFound", "No existe el activo multimedia indicado."));
        }

        // La regla de qué estados se pueden liberar vive en el dominio.
        if (!asset.Release())
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.Conflict("Media.NotQuarantined", "Solo se puede liberar contenido que está en cuarentena."));
        }

        await _mediaAssetRepository.UpdateAsync(asset, cancellationToken);

        DateTime now = _dateTimeProvider.UtcNow;

        // Registro de la decisión manual de moderación: sin URL ni contenido, solo identificadores y acción.
        _logger.LogInformation(
            "Moderación manual: activo {MediaId} liberado de cuarentena por el revisor {ReviewerId}. Acción {Action}.",
            asset.Id,
            _currentUser.UserId,
            "Release");

        return Result.Success(new ReleasedMediaDto(asset.Id, asset.Status.ToString(), asset.SecureUrl, now));
    }
}
