using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Media.Delivery;

/// <summary>
/// Entrega firmada de un activo multimedia aprobado (Spec 023).
/// Ref: FR-023-01 - entrega de contenido; FR-023-02 - validación de firma y TTL; CA-023-03 - 404 si retirado.
/// </summary>
public sealed record GetMediaDeliveryQuery(Guid MediaId, long ExpiresUnixSeconds, string? Signature)
    : IRequest<Result<MediaDeliveryResponse>>;

/// <summary>Ref: FR-023-02 - el identificador del activo es obligatorio.</summary>
public sealed class GetMediaDeliveryQueryValidator : AbstractValidator<GetMediaDeliveryQuery>
{
    public GetMediaDeliveryQueryValidator()
    {
        RuleFor(x => x.MediaId)
            .NotEmpty()
            .WithMessage("El identificador del activo multimedia es obligatorio.");
    }
}

/// <summary>
/// Handler de entrega multimedia vía proxy seguro (B1, B2).
/// Revalida vigencia, estado Approved en base de datos y firma HMAC en cada petición (fail-closed).
/// Aplica cabeceras Cache-Control estrictamente acotadas al tiempo remanente de la firma (CA-023-03).
/// </summary>
public sealed class GetMediaDeliveryQueryHandler
    : IRequestHandler<GetMediaDeliveryQuery, Result<MediaDeliveryResponse>>
{
    private readonly IMediaAssetRepository _mediaAssets;
    private readonly IMediaDeliveryUrlGenerator _generator;
    private readonly IMediaContentProxy _contentProxy;
    private readonly IDateTimeProvider _clock;

    public GetMediaDeliveryQueryHandler(
        IMediaAssetRepository mediaAssets,
        IMediaDeliveryUrlGenerator generator,
        IMediaContentProxy contentProxy,
        IDateTimeProvider clock)
    {
        _mediaAssets = mediaAssets;
        _generator = generator;
        _contentProxy = contentProxy;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<MediaDeliveryResponse>> Handle(
        GetMediaDeliveryQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ExpiresUnixSeconds <= 0 || string.IsNullOrWhiteSpace(request.Signature))
        {
            return Result.Failure<MediaDeliveryResponse>(
                Error.Forbidden("Media.SignatureRequired", "La URL de entrega requiere firma y vigencia."));
        }

        long nowSeconds = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
        if (request.ExpiresUnixSeconds < nowSeconds)
        {
            return Result.Failure<MediaDeliveryResponse>(
                Error.Forbidden("Media.SignatureExpired", "La URL firmada ha expirado."));
        }

        MediaAsset? asset = await _mediaAssets.GetByIdAsync(request.MediaId, cancellationToken);
        // CA-023-03 / B1: un activo retirado o que no esté en estado Approved responde 404
        if (asset is null || asset.Status != MediaAssetStatus.Approved)
        {
            return Result.Failure<MediaDeliveryResponse>(
                Error.NotFound("Media.NotFound", "El activo multimedia no existe o no está disponible."));
        }

        if (!_generator.Validate(request.MediaId, request.ExpiresUnixSeconds, request.Signature, MediaDeliveryScope.Public))
        {
            return Result.Failure<MediaDeliveryResponse>(
                Error.Forbidden("Media.InvalidSignature", "La firma de la URL no es válida."));
        }

        if (string.IsNullOrWhiteSpace(asset.SecureUrl))
        {
            return Result.Failure<MediaDeliveryResponse>(
                Error.Unavailable("Media.ContentUnavailable", "El contenido multimedia no está disponible."));
        }

        Result<MediaContentPayload> content = await _contentProxy.OpenReadAsync(asset, cancellationToken);
        if (content.IsFailure)
        {
            return Result.Failure<MediaDeliveryResponse>(content.Error);
        }

        long remainingSeconds = Math.Clamp(request.ExpiresUnixSeconds - nowSeconds, 0, 86400);
        string cacheControl = remainingSeconds > 0
            ? $"public, max-age={remainingSeconds}, s-maxage={remainingSeconds}"
            : "no-cache, no-store, must-revalidate";

        return Result.Success(MediaDeliveryResponse.Streamed(
            content.Value.Content,
            content.Value.ContentType,
            cacheControl));
    }
}
