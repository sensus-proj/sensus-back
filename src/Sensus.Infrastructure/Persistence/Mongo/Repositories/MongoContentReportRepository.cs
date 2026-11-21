using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB de reportes de contenido con deduplicación e índice único por ventana (Spec 040).
/// </summary>
public sealed class MongoContentReportRepository : IContentReportRepository
{
    private readonly MongoDbContext _context;

    public MongoContentReportRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsRecentAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        string targetTypeValue = targetType.ToString();
        long count = await _context.ContentReports.CountDocumentsAsync(
            r => r.ReporterUserId == reporterUserId
                 && r.TargetType == targetTypeValue
                 && r.TargetId == targetId
                 && r.CreatedAtUtc >= sinceUtc,
            cancellationToken: cancellationToken);

        return count > 0;
    }

    public async Task<bool> TryAddAsync(ContentReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ContentReports.InsertOneAsync(
                ContentReportDocument.FromDomain(report),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<int> CountRecentByTargetAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        string targetTypeValue = targetType.ToString();
        long count = await _context.ContentReports.CountDocumentsAsync(
            r => r.TargetType == targetTypeValue
                 && r.TargetId == targetId
                 && r.CreatedAtUtc >= sinceUtc,
            cancellationToken: cancellationToken);

        return (int)count;
    }
}
