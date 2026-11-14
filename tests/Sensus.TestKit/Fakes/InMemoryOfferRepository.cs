using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>
/// Repositorio en memoria de ofertas para pruebas E2E sin conexión a MongoDB.
/// </summary>
public class InMemoryOfferRepository : IOfferRepository
{
    private readonly ConcurrentDictionary<Guid, Offer> _offers = new();

    public Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _offers.TryGetValue(id, out Offer? offer);
        return Task.FromResult(offer);
    }

    public Task<IReadOnlyList<Offer>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Offer> list = _offers.Values
            .Where(o => o.MerchantId == merchantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToList();

        return Task.FromResult(list);
    }

    public Task AddAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        _offers[offer.Id] = offer;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        _offers[offer.Id] = offer;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateWithVersionCheckAsync(
        Offer offer,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (!_offers.TryGetValue(offer.Id, out Offer? current) || current.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        _offers[offer.Id] = offer;
        return Task.FromResult(true);
    }

    public Task<int> ExpireOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        int expired = 0;
        foreach (Offer offer in _offers.Values)
        {
            if (offer.Status is OfferStatus.Published or OfferStatus.Paused && offer.EndsAt <= nowUtc)
            {
                offer.Expire(nowUtc);
                expired++;
            }
        }

        return Task.FromResult(expired);
    }

    public Task<NearbyOffersPage> GetNearbyAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        MerchantCategory? category,
        int pageNumber,
        int pageSize,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NearbyOffersPage([], 0));
    }

    public Task<bool> TryReserveStockAsync(
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_offers.TryGetValue(offerId, out Offer? offer) || !offer.IsActive(nowUtc))
        {
            return Task.FromResult(false);
        }

        Offer updated = Offer.Reconstitute(
            offer.Id,
            offer.MerchantId,
            offer.Title,
            offer.Description,
            offer.OriginalPrice,
            offer.DiscountPercentage,
            offer.DiscountedPrice,
            offer.Stock - 1,
            offer.StartsAt,
            offer.EndsAt,
            offer.Status,
            offer.ImageUrl,
            offer.Latitude,
            offer.Longitude,
            offer.CreatedAtUtc,
            nowUtc,
            offer.ViewCount,
            offer.Version,
            offer.Category);

        _offers[offer.Id] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> ReturnStockAsync(
        Guid offerId,
        int units,
        CancellationToken cancellationToken = default)
    {
        if (!_offers.TryGetValue(offerId, out Offer? offer) || offer.ReturnStock(units).IsFailure)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> IncrementViewCountAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        if (!_offers.TryGetValue(offerId, out Offer? offer) || offer.RegisterView().IsFailure)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task UpdateMerchantLocationAsync(
        Guid merchantId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        foreach (Offer offer in _offers.Values.Where(o => o.MerchantId == merchantId).ToList())
        {
            _offers[offer.Id] = Offer.Reconstitute(
                offer.Id,
                offer.MerchantId,
                offer.Title,
                offer.Description,
                offer.OriginalPrice,
                offer.DiscountPercentage,
                offer.DiscountedPrice,
                offer.Stock,
                offer.StartsAt,
                offer.EndsAt,
                offer.Status,
                offer.ImageUrl,
                latitude,
                longitude,
                offer.CreatedAtUtc,
                DateTime.UtcNow,
                offer.ViewCount,
                offer.Version,
                offer.Category);
        }

        return Task.CompletedTask;
    }
}
