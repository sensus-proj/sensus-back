using System.Globalization;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Registro de reporte comunitario de contenido (Spec 040).
/// Nunca debe exponerse el identificador del reportante en respuestas públicas ni logs.
/// </summary>
public sealed class ContentReport : BaseEntity
{
    public const int MaxDetailsLength = 500;

    public Guid ReporterUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Details { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string WindowBucket { get; private set; } = string.Empty;

    private ContentReport()
    {
    }

    private ContentReport(
        Guid id,
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? details,
        DateTime createdAtUtc,
        string windowBucket) : base(id)
    {
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Details = details;
        CreatedAtUtc = createdAtUtc;
        WindowBucket = windowBucket;
    }

    public static Result<ContentReport> Create(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? details,
        DateTime createdAtUtc,
        Guid? id = null)
    {
        if (reporterUserId == Guid.Empty)
        {
            return Result.Failure<ContentReport>(
                Error.Validation("ContentReport.InvalidReporter", "El usuario reportante es obligatorio."));
        }

        if (targetId == Guid.Empty)
        {
            return Result.Failure<ContentReport>(
                Error.Validation("ContentReport.InvalidTarget", "El identificador del contenido es obligatorio."));
        }

        string? normalizedDetails = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (normalizedDetails is not null && normalizedDetails.Length > MaxDetailsLength)
        {
            return Result.Failure<ContentReport>(
                Error.Validation("ContentReport.DetailsTooLong", $"El detalle no debe exceder {MaxDetailsLength} caracteres."));
        }

        if (ContentReportPolicy.IsProhibitedLegalContent(reason.ToApiCode(), normalizedDetails))
        {
            return Result.Failure<ContentReport>(
                Error.Validation(
                    "ContentReport.ProhibitedLegalContent",
                    "Este tipo de contenido está fuera del alcance de moderación de la plataforma y debe reportarse ante las autoridades competentes."));
        }

        DateTime utcTimestamp = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);

        string windowBucket = utcTimestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return Result.Success(new ContentReport(
            id ?? Guid.NewGuid(),
            reporterUserId,
            targetType,
            targetId,
            reason,
            normalizedDetails,
            utcTimestamp,
            windowBucket));
    }
}
