using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Ai.Services;

/// <summary>
/// FR-046-18: completa con datos confiables del repositorio (no del modelo) las ofertas que la respuesta de
/// Sensi IA menciona, para que el cliente pueda mostrar la información completa y las acciones disponibles.
/// Cualquier fallo de lectura degrada a los datos básicos de la oferta, sin romper el turno.
/// </summary>
public sealed class CitedOfferEnricher : ICitedOfferEnricher
{
    private const double EarthRadiusKm = 6371.0088;

    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SensiSettings _settings;
    private readonly ILogger<CitedOfferEnricher> _logger;

    public CitedOfferEnricher(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IDateTimeProvider dateTimeProvider,
        IOptions<SensiSettings> settings,
        ILogger<CitedOfferEnricher> logger)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CitedOfferDto>> EnrichAsync(
        IReadOnlyList<CitedOfferDto> citedOffers,
        string reply,
        IReadOnlySet<string> excludedOfferIds,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        if (citedOffers.Count == 0 || string.IsNullOrWhiteSpace(reply))
        {
            return [];
        }

        var mentioned = citedOffers
            .Where(o => !excludedOfferIds.Contains(o.OfferId)
                && !string.IsNullOrWhiteSpace(o.Title)
                && reply.Contains(o.Title.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(_settings.MaxCitedOfferCards)
            .ToList();

        var merchants = new Dictionary<Guid, Comercio?>();
        var result = new List<CitedOfferDto>(mentioned.Count);
        foreach (var cited in mentioned)
        {
            result.Add(await EnrichOneAsync(cited, merchants, latitude, longitude, cancellationToken));
        }

        return result;
    }

    private async Task<CitedOfferDto> EnrichOneAsync(
        CitedOfferDto cited,
        Dictionary<Guid, Comercio?> merchants,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(cited.OfferId, out var offerId))
        {
            return cited;
        }

        try
        {
            var offer = await _offerRepository.GetByIdAsync(offerId, cancellationToken);
            if (offer is null)
            {
                return cited;
            }

            if (!merchants.TryGetValue(offer.MerchantId, out var merchant))
            {
                merchant = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);
                merchants[offer.MerchantId] = merchant;
            }

            var offerLatitude = offer.Latitude ?? merchant?.Latitud;
            var offerLongitude = offer.Longitude ?? merchant?.Longitud;

            return cited with
            {
                Title = offer.Title,
                MerchantId = offer.MerchantId.ToString(),
                MerchantName = merchant?.NombreComercial ?? cited.MerchantName,
                OfferPriceMxn = offer.DiscountedPrice,
                RegularPriceMxn = offer.OriginalPrice,
                AvailableStock = offer.Stock,
                IsOpenNow = offer.IsActive(_dateTimeProvider.UtcNow),
                DistanceKm = DistanceKm(latitude, longitude, offerLatitude, offerLongitude),
                Description = offer.Description,
                DiscountPercentage = offer.DiscountPercentage,
                ImageUrl = offer.ImageUrl,
                Category = offer.Category,
                EndsAtUtc = offer.EndsAt,
                MerchantAddress = merchant?.Direccion,
                Latitude = offerLatitude,
                Longitude = offerLongitude
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo completar la oferta citada de Sensi IA ({ErrorType}).", ex.GetType().Name);
            return cited;
        }
    }

    private static double? DistanceKm(double? fromLat, double? fromLon, double? toLat, double? toLon)
    {
        if (!fromLat.HasValue || !fromLon.HasValue || !toLat.HasValue || !toLon.HasValue)
        {
            return null;
        }

        var dLat = ToRadians(toLat.Value - fromLat.Value);
        var dLon = ToRadians(toLon.Value - fromLon.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(fromLat.Value)) * Math.Cos(ToRadians(toLat.Value))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return Math.Round(EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
