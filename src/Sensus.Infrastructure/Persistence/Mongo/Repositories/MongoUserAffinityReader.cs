using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Agrega la afinidad inferida por categoría desde vistas (90 días, peso 1), apartados (TTL 30 días, peso 3)
/// y favoritos (peso 2, Spec 038) del usuario usando consultas indexadas y caché Redis
/// <c>feed:affinity:{userId}</c> (Spec 042, FR-042-04).
/// </summary>
public sealed class MongoUserAffinityReader : IUserAffinityReader
{
    private const int ViewLookbackDays = 90;
    private const double ViewWeight = 1.0d;
    private const double ClaimWeight = 3.0d;
    private const double FavoriteWeight = 2.0d;

    private readonly MongoDbContext _context;
    private readonly IDateTimeProvider _clock;
    private readonly ICacheService? _cacheService;
    private readonly FeedRankingSettings _settings;
    private readonly ILogger<MongoUserAffinityReader> _logger;

    public MongoUserAffinityReader(
        MongoDbContext context,
        IDateTimeProvider clock,
        IOptions<FeedRankingSettings> settings,
        ILogger<MongoUserAffinityReader> logger,
        ICacheService? cacheService = null)
    {
        _context = context;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyDictionary<string, double>> GetCategoryAffinitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new Dictionary<string, double>();
        }

        string cacheKey = FeedCacheKeys.Affinity(userId);
        if (_cacheService is not null)
        {
            Dictionary<string, double>? cached = await _cacheService.GetAsync<Dictionary<string, double>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        Dictionary<string, double> computed = await ComputeFromSignalsAsync(userId, cancellationToken);

        if (_cacheService is not null)
        {
            int ttlMinutes = _settings.AffinityCacheMinutes > 0 ? _settings.AffinityCacheMinutes : 10;
            await _cacheService.SetAsync(cacheKey, computed, TimeSpan.FromMinutes(ttlMinutes), cancellationToken);
        }

        return computed;
    }

    private async Task<Dictionary<string, double>> ComputeFromSignalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            DateTime viewCutoff = _clock.UtcNow.AddDays(-ViewLookbackDays);

            FilterDefinition<OfferViewDocument> viewFilter = Builders<OfferViewDocument>.Filter.And(
                Builders<OfferViewDocument>.Filter.Eq(v => v.UserId, userId),
                Builders<OfferViewDocument>.Filter.Gte(v => v.CreatedAtUtc, viewCutoff));

            List<Guid> viewedOfferIds = await _context.OfferViews
                .Find(viewFilter)
                .Project(v => v.OfferId)
                .ToListAsync(cancellationToken);

            FilterDefinition<OfferClaimDocument> claimFilter =
                Builders<OfferClaimDocument>.Filter.Eq(c => c.UserId, userId);

            List<Guid> claimedOfferIds = await _context.OfferClaims
                .Find(claimFilter)
                .Project(c => c.OfferId)
                .ToListAsync(cancellationToken);

            List<Guid> favoritedOfferIds = await _context.OfferFavorites
                .Find(f => f.UserId == userId)
                .Project(f => f.OfferId)
                .ToListAsync(cancellationToken);

            if (viewedOfferIds.Count == 0 && claimedOfferIds.Count == 0 && favoritedOfferIds.Count == 0)
            {
                return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }

            var offerWeights = new Dictionary<Guid, double>();
            foreach (Guid offerId in viewedOfferIds)
            {
                offerWeights[offerId] = offerWeights.GetValueOrDefault(offerId) + ViewWeight;
            }

            foreach (Guid offerId in claimedOfferIds)
            {
                offerWeights[offerId] = offerWeights.GetValueOrDefault(offerId) + ClaimWeight;
            }

            foreach (Guid offerId in favoritedOfferIds)
            {
                offerWeights[offerId] = offerWeights.GetValueOrDefault(offerId) + FavoriteWeight;
            }

            FilterDefinition<OfferDocument> offerFilter =
                Builders<OfferDocument>.Filter.In(o => o.Id, offerWeights.Keys);

            var offers = await _context.Offers
                .Find(offerFilter)
                .Project(o => new { o.Id, o.Category })
                .ToListAsync(cancellationToken);

            var categoryScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var offer in offers)
            {
                if (!OfferCategoryCatalog.TryNormalizeLegacy(offer.Category, out OfferCategory parsed))
                {
                    continue;
                }

                string key = parsed.ToString();
                double weight = offerWeights.GetValueOrDefault(offer.Id);
                categoryScores[key] = categoryScores.GetValueOrDefault(key) + weight;
            }

            if (categoryScores.Count == 0)
            {
                return categoryScores;
            }

            double maxScore = categoryScores.Values.Max();
            if (maxScore <= 0d)
            {
                return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (string key in categoryScores.Keys.ToList())
            {
                categoryScores[key] = Math.Round(Math.Clamp(categoryScores[key] / maxScore, 0d, 1d), 6);
            }

            return categoryScores;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Degradación limpia: si la lectura de afinidad falla, el feed continúa con las demás señales.
            _logger.LogWarning(ex, "Error al calcular la afinidad inferida por categorías; degradando sin señales históricas.");
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
