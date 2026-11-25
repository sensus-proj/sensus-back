using System.Security.Cryptography;
using System.Text;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Domain.Entities;

/// <summary>
/// Copia anonimizada de la evidencia de una decisión de moderación, conservada 90 días tras la baja
/// de cuenta (Spec 061, FR-061-05). No contiene correo, nombre, foto ni identificador directo del usuario:
/// <see cref="SubjectKey"/> es un hash SHA-256 del identificador y el resto de campos son metadatos
/// técnicos de <see cref="SanctionEvidence"/>. La colección de sanciones original no se modifica.
/// </summary>
public sealed class ModerationAudit : BaseEntity
{
    private ModerationAudit()
    {
    }

    public string SubjectKey { get; private set; } = string.Empty;
    public Guid SourceSanctionId { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public IReadOnlyDictionary<string, double> Scores { get; private set; } =
        new Dictionary<string, double>();
    public string? ThresholdApplied { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Seudónimo irreversible del usuario; nunca se persiste el identificador original.</summary>
    public static string ComputeSubjectKey(Guid userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D"))))
            .ToLowerInvariant();

    /// <summary>
    /// Crea la copia anonimizada de una sanción automática. Las sanciones manuales no aportan evidencia
    /// de moderación y se omiten. El identificador es determinista para garantizar idempotencia.
    /// </summary>
    public static ModerationAudit? FromSanction(
        Sanction sanction,
        string subjectKey,
        TimeSpan retention,
        DateTime nowUtc)
    {
        if (sanction.Type == SanctionType.Manual ||
            string.Equals(sanction.Evidence.Decision, SanctionEvidence.Manual.Decision, StringComparison.Ordinal))
        {
            return null;
        }

        byte[] idHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{subjectKey}:{sanction.Id:D}"));

        return new ModerationAudit
        {
            Id = new Guid(idHash.AsSpan(0, 16)),
            SubjectKey = subjectKey,
            SourceSanctionId = sanction.Id,
            Decision = sanction.Evidence.Decision,
            Categories = sanction.Evidence.Categories.ToArray(),
            Scores = new Dictionary<string, double>(sanction.Evidence.Scores),
            ThresholdApplied = sanction.Evidence.ThresholdApplied,
            Action = sanction.Evidence.Action,
            RecordedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(nowUtc.Add(retention), DateTimeKind.Utc)
        };
    }
}
