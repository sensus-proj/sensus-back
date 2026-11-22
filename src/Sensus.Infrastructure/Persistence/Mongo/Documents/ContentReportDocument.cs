using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de reporte de contenido en MongoDB Atlas (Spec 040).
/// </summary>
public sealed class ContentReportDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("reporterUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ReporterUserId { get; set; }

    [BsonElement("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("targetId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TargetId { get; set; }

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("details")]
    public string? Details { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("windowBucket")]
    public string WindowBucket { get; set; } = string.Empty;

    public static ContentReportDocument FromDomain(ContentReport report) =>
        new()
        {
            Id = report.Id,
            ReporterUserId = report.ReporterUserId,
            TargetType = report.TargetType.ToString(),
            TargetId = report.TargetId,
            Reason = report.Reason.ToString(),
            Details = report.Details,
            CreatedAtUtc = report.CreatedAtUtc,
            WindowBucket = report.WindowBucket
        };
}
