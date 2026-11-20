using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Migration;

/// <summary>
/// Evidencia cuantitativa de la migración de categorías de ofertas (Spec 034, FR-034-09, T034-14).
/// </summary>
public sealed record OfferCategoryMigrationResult(
    int TotalScanned,
    int ConvertedCount,
    int AlreadyCanonicalCount,
    int NulledCount,
    int AlreadyNullCount,
    bool AlreadyExecuted,
    DateTime ExecutedAtUtc);

/// <summary>
/// Migración única e idempotente de categorías legadas en español hacia el catálogo oficial
/// <see cref="OfferCategory"/> en la colección <c>offers</c> (FR-034-09).
/// </summary>
public sealed class OfferCategoryMigrationService
{
    public const string MigrationId = "offer_category_v1";
    private const int BatchSize = 200;

    private readonly MongoDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<OfferCategoryMigrationService> _logger;

    public OfferCategoryMigrationService(
        MongoDbContext context,
        IDateTimeProvider dateTimeProvider,
        ILogger<OfferCategoryMigrationService> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<OfferCategoryMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var existingFilter = Builders<SchemaMigrationDocument>.Filter.Eq(m => m.Id, MigrationId);
        SchemaMigrationDocument? existing = await _context.SchemaMigrations
            .Find(existingFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Migración {MigrationId} ya fue ejecutada el {ExecutedAtUtc}. Se omite re-ejecución.",
                MigrationId,
                existing.ExecutedAtUtc);

            return new OfferCategoryMigrationResult(
                existing.TotalScanned,
                existing.ConvertedCount,
                existing.AlreadyCanonicalCount,
                existing.NulledCount,
                existing.AlreadyNullCount,
                AlreadyExecuted: true,
                existing.ExecutedAtUtc);
        }

        int totalScanned = 0;
        int convertedCount = 0;
        int alreadyCanonicalCount = 0;
        int nulledCount = 0;
        int alreadyNullCount = 0;

        var pendingUpdates = new List<WriteModel<OfferDocument>>(BatchSize);

        var projection = Builders<OfferDocument>.Projection
            .Include(o => o.Id)
            .Include(o => o.Category);

        using var cursor = await _context.Offers
            .Find(FilterDefinition<OfferDocument>.Empty)
            .Project<OfferDocument>(projection)
            .ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (OfferDocument doc in cursor.Current)
            {
                totalScanned++;
                (string? targetCategory, CategoryResolutionKind kind) = ClassifyCategory(doc.Category);

                switch (kind)
                {
                    case CategoryResolutionKind.AlreadyNull:
                        alreadyNullCount++;
                        break;
                    case CategoryResolutionKind.AlreadyCanonical:
                        alreadyCanonicalCount++;
                        break;
                    case CategoryResolutionKind.Converted:
                        convertedCount++;
                        EnqueueCategoryUpdate(pendingUpdates, doc.Id, targetCategory);
                        break;
                    case CategoryResolutionKind.Nulled:
                        nulledCount++;
                        EnqueueCategoryUpdate(pendingUpdates, doc.Id, null);
                        break;
                }

                if (pendingUpdates.Count >= BatchSize)
                {
                    await _context.Offers.BulkWriteAsync(
                        pendingUpdates,
                        new BulkWriteOptions { IsOrdered = false },
                        cancellationToken);
                    pendingUpdates.Clear();
                }
            }
        }

        if (pendingUpdates.Count > 0)
        {
            await _context.Offers.BulkWriteAsync(
                pendingUpdates,
                new BulkWriteOptions { IsOrdered = false },
                cancellationToken);
        }

        DateTime executedAtUtc = _dateTimeProvider.UtcNow;
        var marker = new SchemaMigrationDocument
        {
            Id = MigrationId,
            ExecutedAtUtc = executedAtUtc,
            TotalScanned = totalScanned,
            ConvertedCount = convertedCount,
            AlreadyCanonicalCount = alreadyCanonicalCount,
            NulledCount = nulledCount,
            AlreadyNullCount = alreadyNullCount
        };

        await _context.SchemaMigrations.ReplaceOneAsync(
            existingFilter,
            marker,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        _logger.LogInformation(
            "Migración {MigrationId} completada. Escaneadas={TotalScanned}, Convertidas={ConvertedCount}, YaCanónicas={AlreadyCanonicalCount}, Anuladas={NulledCount}, YaNulas={AlreadyNullCount}.",
            MigrationId,
            totalScanned,
            convertedCount,
            alreadyCanonicalCount,
            nulledCount,
            alreadyNullCount);

        return new OfferCategoryMigrationResult(
            totalScanned,
            convertedCount,
            alreadyCanonicalCount,
            nulledCount,
            alreadyNullCount,
            AlreadyExecuted: false,
            executedAtUtc);
    }

    private static void EnqueueCategoryUpdate(
        List<WriteModel<OfferDocument>> updates,
        Guid offerId,
        string? newCategory)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, offerId);
        var update = Builders<OfferDocument>.Update.Set(o => o.Category, newCategory);
        updates.Add(new UpdateOneModel<OfferDocument>(filter, update));
    }

    internal static (string? TargetCategory, CategoryResolutionKind Kind) ClassifyCategory(string? rawCategory)
    {
        if (rawCategory is null)
        {
            return (null, CategoryResolutionKind.AlreadyNull);
        }

        if (OfferCategoryCatalog.TryParse(rawCategory, out OfferCategory canonical))
        {
            string canonicalName = canonical.ToString();
            return string.Equals(rawCategory, canonicalName, StringComparison.Ordinal)
                ? (canonicalName, CategoryResolutionKind.AlreadyCanonical)
                : (canonicalName, CategoryResolutionKind.Converted);
        }

        if (OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory normalized))
        {
            return (normalized.ToString(), CategoryResolutionKind.Converted);
        }

        return (null, CategoryResolutionKind.Nulled);
    }

    internal enum CategoryResolutionKind
    {
        AlreadyNull,
        AlreadyCanonical,
        Converted,
        Nulled
    }
}
