namespace Sensus.Application.Reports.DTOs;

/// <summary>
/// Respuesta pública tras registrar un reporte de contenido (Spec 040).
/// Omite intencionalmente cualquier dato identificable del reportante.
/// </summary>
public sealed record ContentReportDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    bool TargetMarkedForReview,
    DateTime CreatedAtUtc,
    string Message);
