using System.Collections.Concurrent;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryReportTargetService : IReportTargetService
{
    private readonly ConcurrentDictionary<(ReportTargetType, Guid), ReportTargetInfo> _targets = new();
    private readonly ConcurrentDictionary<(ReportTargetType, Guid), DateTime> _flagged = new();

    public void RegisterTarget(ReportTargetType targetType, Guid targetId, Guid? authorUserId) =>
        _targets[(targetType, targetId)] = new ReportTargetInfo(true, authorUserId);

    public bool IsFlaggedUnderReview(ReportTargetType targetType, Guid targetId) =>
        _flagged.ContainsKey((targetType, targetId));

    public Task<ReportTargetInfo> GetTargetInfoAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_targets.TryGetValue((targetType, targetId), out ReportTargetInfo? info)
            ? info
            : new ReportTargetInfo(false, null));

    public Task<bool> MarkUnderReviewAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime flaggedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_targets.ContainsKey((targetType, targetId)))
        {
            return Task.FromResult(false);
        }

        _flagged[(targetType, targetId)] = flaggedAtUtc;
        return Task.FromResult(true);
    }
}
