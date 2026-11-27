using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers;

/// <summary>
/// Resuelve el comercio propietario y valida titularidad sobre una oferta en comandos y consultas del inventario.
/// </summary>
internal static class MerchantOfferAccessHelper
{
    internal static async Task<Result<(Guid UserId, Comercio Comercio)>> ResolveMerchantAsync(
        ICurrentUserService currentUserService,
        IComercioRepository comercioRepository,
        string unauthorizedMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserService.UserId) ||
            !Guid.TryParse(currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<(Guid, Comercio)>(
                Error.Unauthorized("Offer.Unauthorized", unauthorizedMessage));
        }

        Comercio? comercio = await comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null)
        {
            return Result.Failure<(Guid, Comercio)>(
                Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        return Result.Success((userId, comercio));
    }

    internal static async Task<Result<(Guid UserId, Comercio Comercio, Offer Offer)>> ResolveOwnedOfferAsync(
        ICurrentUserService currentUserService,
        IComercioRepository comercioRepository,
        IOfferRepository offerRepository,
        Guid offerId,
        string unauthorizedMessage,
        string forbiddenMessage,
        CancellationToken cancellationToken)
    {
        var merchantResult = await ResolveMerchantAsync(
            currentUserService,
            comercioRepository,
            unauthorizedMessage,
            cancellationToken);
        if (merchantResult.IsFailure)
        {
            return Result.Failure<(Guid, Comercio, Offer)>(merchantResult.Error);
        }

        (Guid userId, Comercio comercio) = merchantResult.Value;
        Offer? offer = await offerRepository.GetByIdAsync(offerId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<(Guid, Comercio, Offer)>(
                Error.NotFound("Offer.NotFound", "La oferta especificada no existe."));
        }

        if (offer.MerchantId != comercio.Id)
        {
            return Result.Failure<(Guid, Comercio, Offer)>(
                Error.Forbidden("Offer.Forbidden", forbiddenMessage));
        }

        return Result.Success((userId, comercio, offer));
    }
}
