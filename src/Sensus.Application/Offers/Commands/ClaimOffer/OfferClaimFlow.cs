using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ClaimOffer;

/// <summary>
/// Flujo compartido para reservar stock atómicamente (1..N unidades y 1..M productos de una misma tienda)
/// y emitir un único código QR / apartado con compensación completa ante cualquier fallo (Spec 033).
/// </summary>
internal static class OfferClaimFlow
{
    public static IReadOnlyList<OfferClaimItemDto> ToItemDtos(OfferClaim claim) =>
        claim.EffectiveItems
            .Select(item => new OfferClaimItemDto(item.OfferId, item.Quantity, item.OfferTitle, item.UnitPrice))
            .ToList();

    public static async Task<Result<ClaimOfferResultDto>> ExecuteAsync(
        IReadOnlyList<CartClaimItemInput> rawItems,
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService,
        IClaimCodeService claimCodeService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService,
        IClaimPushNotifier? claimPushNotifier,
        IOfferInteractionRecorder? interactionRecorder,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserService.UserId) ||
            !Guid.TryParse(currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Unauthorized("OfferClaim.Unauthorized", "Debe iniciar sesión para apartar una oferta."));
        }

        if (rawItems is null || rawItems.Count == 0 || rawItems.Any(i => i.OfferId == Guid.Empty || i.Quantity < 1))
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Validation("OfferClaim.InvalidItems", "Selecciona al menos un producto y cantidad válida para apartar."));
        }

        var normalizedInputs = rawItems
            .GroupBy(item => item.OfferId)
            .Select(group => new CartClaimItemInput(group.Key, group.Sum(x => x.Quantity)))
            .ToList();

        DateTime now = dateTimeProvider.UtcNow;
        var resolvedOffers = new List<(Offer Offer, int Quantity)>(normalizedInputs.Count);
        foreach (CartClaimItemInput input in normalizedInputs)
        {
            Offer? offer = await offerRepository.GetByIdAsync(input.OfferId, cancellationToken);
            if (offer is null || !offer.IsActive(now))
            {
                return Result.Failure<ClaimOfferResultDto>(
                    Error.Conflict("OfferClaim.OfferNotAvailable", "La oferta no está disponible para apartar."));
            }

            resolvedOffers.Add((offer, input.Quantity));
        }

        Guid merchantId = resolvedOffers[0].Offer.MerchantId;
        if (resolvedOffers.Any(pair => pair.Offer.MerchantId != merchantId))
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Validation(
                    "OfferClaim.MultipleMerchants",
                    "Solo puedes generar un apartado con productos de una misma tienda."));
        }

        OfferClaim? existingClaim = resolvedOffers.Count == 1
            ? await offerClaimRepository.GetActiveByUserAndOfferAsync(userId, resolvedOffers[0].Offer.Id, cancellationToken)
            : await offerClaimRepository.GetActiveByUserAndAnyOfferAsync(
                userId,
                resolvedOffers.Select(x => x.Offer.Id),
                cancellationToken);

        if (existingClaim is not null)
        {
            string conflictMessage = resolvedOffers.Count == 1
                ? "Ya tiene un apartado activo para esta oferta."
                : "Ya tienes un apartado activo para al menos uno de los productos seleccionados.";
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.AlreadyActive", conflictMessage));
        }

        var reserved = new List<(Guid OfferId, int Quantity)>(resolvedOffers.Count);
        foreach ((Offer offer, int quantity) in resolvedOffers)
        {
            bool ok = quantity == 1
                ? await offerRepository.TryReserveStockAsync(offer.Id, now, cancellationToken)
                : await offerRepository.TryReserveStockAsync(offer.Id, quantity, now, cancellationToken);

            if (!ok)
            {
                await RollbackStockAsync(offerRepository, reserved);
                return Result.Failure<ClaimOfferResultDto>(
                    Error.Conflict("OfferClaim.NoStock", "La oferta ya no tiene stock disponible."));
            }

            reserved.Add((offer.Id, quantity));
        }

        List<OfferClaimItem> claimItems = resolvedOffers
            .Select(pair => new OfferClaimItem(
                pair.Offer.Id,
                pair.Quantity,
                pair.Offer.Title,
                pair.Offer.DiscountedPrice))
            .ToList();

        Result<OfferClaim> claimResult = OfferClaim.CreateCart(userId, merchantId, claimItems, now);
        if (claimResult.IsFailure)
        {
            await RollbackStockAsync(offerRepository, reserved);
            return Result.Failure<ClaimOfferResultDto>(claimResult.Error);
        }

        OfferClaim claim = claimResult.Value;
        ClaimCode generatedCode = claimCodeService.Generate(claim.Id, claim.OfferId, claim.ExpiresAtUtc);
        Result hashResult = claim.AssignCodeHash(generatedCode.Hash);
        if (hashResult.IsFailure)
        {
            await RollbackStockAsync(offerRepository, reserved);
            return Result.Failure<ClaimOfferResultDto>(hashResult.Error);
        }

        bool inserted = await offerClaimRepository.TryAddAsync(claim, cancellationToken);
        if (!inserted)
        {
            await RollbackStockAsync(offerRepository, reserved);
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.AlreadyActive", "Ya tiene un apartado activo para esta oferta."));
        }

        if (cacheService is not null)
        {
            foreach ((Offer offer, _) in resolvedOffers)
            {
                await cacheService.InvalidateAsync(offer.Id, cancellationToken);
            }
        }

        if (claimPushNotifier is not null)
        {
            await claimPushNotifier.NotifyAsync(claim, cancellationToken);
        }

        if (!currentUserService.IsGuest && interactionRecorder is not null)
        {
            for (int index = 0; index < resolvedOffers.Count; index++)
            {
                Offer offer = resolvedOffers[index].Offer;
                Result<OfferInteractionItem> itemResult = OfferInteractionItem.Create(
                    offer.Id,
                    position: index + 1,
                    FeedSlot.Direct,
                    offer.Category,
                    DistanceBucket.Unknown);

                if (itemResult.IsSuccess)
                {
                    try
                    {
                        await interactionRecorder.RecordSingleAsync(
                            userId,
                            OfferInteractionType.Claim,
                            itemResult.Value,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger?.LogWarning(ex, "Error no bloqueante registrando interacción de apartado para oferta {OfferId}.", offer.Id);
                    }
                }
            }
        }

        return Result.Success(new ClaimOfferResultDto(
            claim.Id,
            claim.OfferId,
            generatedCode.Code,
            generatedCode.QrPayload,
            claim.ExpiresAtUtc,
            claim.TotalUnits,
            ToItemDtos(claim)));
    }

    private static async Task RollbackStockAsync(
        IOfferRepository offerRepository,
        IReadOnlyList<(Guid OfferId, int Quantity)> reserved)
    {
        foreach ((Guid offerId, int quantity) in reserved)
        {
            await offerRepository.ReturnStockAsync(offerId, quantity, CancellationToken.None);
        }
    }
}
