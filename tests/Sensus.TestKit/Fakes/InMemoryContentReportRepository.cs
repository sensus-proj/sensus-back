using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryContentReportRepository : IContentReportRepository
{
    private readonly List<ContentReport> _reports = [];

    public IReadOnlyList<ContentReport> All => _reports;

    public Task<bool> ExistsRecentAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        bool exists = _reports.Any(r =>
            r.ReporterUserId == reporterUserId
            && r.TargetType == targetType
            && r.TargetId == targetId
            && r.CreatedAtUtc >= sinceUtc);
        return Task.FromResult(exists);
    }

    public Task<bool> TryAddAsync(ContentReport report, CancellationToken cancellationToken = default)
    {
        bool duplicate = _reports.Any(r =>
            r.ReporterUserId == report.ReporterUserId
            && r.TargetType == report.TargetType
            && r.TargetId == report.TargetId
            && r.WindowBucket == report.WindowBucket);

        if (duplicate)
        {
            return Task.FromResult(false);
        }

        _reports.Add(report);
        return Task.FromResult(true);
    }

    public Task<int> CountRecentByTargetAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        int count = _reports.Count(r =>
            r.TargetType == targetType
            && r.TargetId == targetId
            && r.CreatedAtUtc >= sinceUtc);
        return Task.FromResult(count);
    }
}
