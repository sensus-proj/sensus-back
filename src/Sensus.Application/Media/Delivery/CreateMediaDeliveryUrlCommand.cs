using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Media.Delivery;

/// <summary>
/// Emite una URL firmada para entregar un activo multimedia aprobado (Spec 023).
/// Ref: FR-023-01 - entrega por CDN; FR-023-02 - firma de URLs de entrega.
/// </summary>
public sealed record CreateMediaDeliveryUrlCommand(Guid MediaId)
    : IRequest<Result<MediaDeliveryUrlDto>>;

/// <summary>Ref: FR-023-02 - el identificador del activo es obligatorio.</summary>
public sealed class CreateMediaDeliveryUrlCommandValidator : AbstractValidator<CreateMediaDeliveryUrlCommand>
{
    public CreateMediaDeliveryUrlCommandValidator()
    {
        RuleFor(x => x.MediaId)
            .NotEmpty()
            .WithMessage("El identificador del activo multimedia es obligatorio.");
    }
}

/// <summary>
/// Handler de emisión de URLs firmadas. Solo entrega contenido aprobado (fail-closed, B1).
/// Cualquier otro estado (pendiente, cuarentena, rechazado) responde NotFound (CA-023-03).
/// </summary>
public sealed class CreateMediaDeliveryUrlCommandHandler
    : IRequestHandler<CreateMediaDeliveryUrlCommand, Result<MediaDeliveryUrlDto>>
{
    private readonly IMediaAssetRepository _mediaAssets;
    private readonly IMediaDeliveryUrlGenerator _generator;
    private readonly IOptions<MediaDeliverySettings> _settings;

    public CreateMediaDeliveryUrlCommandHandler(
        IMediaAssetRepository mediaAssets,
        IMediaDeliveryUrlGenerator generator,
        IOptions<MediaDeliverySettings> settings)
    {
        _mediaAssets = mediaAssets;
        _generator = generator;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<Result<MediaDeliveryUrlDto>> Handle(
        CreateMediaDeliveryUrlCommand request,
        CancellationToken cancellationToken)
    {
        MediaAsset? asset = await _mediaAssets.GetByIdAsync(request.MediaId, cancellationToken);
        if (asset is null || asset.Status != MediaAssetStatus.Approved)
        {
            return Result.Failure<MediaDeliveryUrlDto>(
                Error.NotFound("Media.NotFound", "El activo multimedia no existe o no está disponible."));
        }

        int ttlMinutes = Math.Max(1, _settings.Value.PublicTtlMinutes);

        Result<MediaDeliveryUrl> urlResult = _generator.Generate(asset, TimeSpan.FromMinutes(ttlMinutes));
        if (urlResult.IsFailure)
        {
            return Result.Failure<MediaDeliveryUrlDto>(urlResult.Error);
        }

        MediaDeliveryUrl url = urlResult.Value;
        return Result.Success(new MediaDeliveryUrlDto(
            asset.Id,
            url.Url,
            url.ExpiresAtUtc,
            url.Scope.ToString(),
            asset.Status.ToString(),
            Math.Max(0, _settings.Value.PublicCacheSeconds)));
    }
}
