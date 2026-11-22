using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Interfaces;

public sealed record ReportTargetInfo(bool Exists, Guid? AuthorUserId);

/// <summary>
/// Verifica existencia/autoría del contenido reportado y lo marca en revisión al superar el umbral (Spec 040).
/// </summary>
public interface IReportTargetService
{
    Task<ReportTargetInfo> GetTargetInfoAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkUnderReviewAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime flaggedAtUtc,
        CancellationToken cancellationToken = default);
}
