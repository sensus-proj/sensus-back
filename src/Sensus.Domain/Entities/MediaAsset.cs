using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa un activo multimedia subido por un usuario.
/// Almacenado en Cloudinary y moderado antes de exponerse públicamente (Spec 022).
/// </summary>
public sealed class MediaAsset : BaseEntity
{
    internal MediaAsset(
        Guid id,
        Guid ownerId,
        string publicId,
        string? secureUrl,
        string contentType,
        MediaAssetStatus status,
        DateTime createdAtUtc)
    {
        Id = id;
        OwnerId = ownerId;
        PublicId = publicId;
        SecureUrl = secureUrl;
        ContentType = contentType;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid OwnerId { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public string? SecureUrl { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public MediaAssetStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static MediaAsset Create(
        Guid ownerId,
        string publicId,
        string? secureUrl,
        string contentType,
        MediaAssetStatus status,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw new ArgumentException("El publicId de Cloudinary es obligatorio.", nameof(publicId));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("El tipo de contenido es obligatorio.", nameof(contentType));
        }

        return new MediaAsset(
            Guid.NewGuid(),
            ownerId,
            publicId.Trim(),
            secureUrl,
            contentType.Trim(),
            status,
            createdAtUtc);
    }

    public void Approve(string? secureUrl = null)
    {
        Status = MediaAssetStatus.Approved;
        if (secureUrl is not null)
        {
            SecureUrl = secureUrl;
        }
    }

    public void Quarantine()
    {
        Status = MediaAssetStatus.Quarantined;
    }

    public void Reject()
    {
        Status = MediaAssetStatus.Rejected;
    }

    /// <summary>
    /// Libera un activo en cuarentena tras su aprobación manual (FR-019-04).
    /// Solo es válido desde <see cref="MediaAssetStatus.Quarantined"/>.
    /// </summary>
    public bool Release()
    {
        if (Status != MediaAssetStatus.Quarantined)
        {
            return false;
        }

        Status = MediaAssetStatus.Approved;
        return true;
    }

    /// <summary>
    /// Indica si el activo en cuarentena superó la retención máxima (FR-019-03).
    /// </summary>
    public bool IsQuarantineExpired(DateTime nowUtc, TimeSpan retention)
    {
        return Status == MediaAssetStatus.Quarantined && nowUtc - CreatedAtUtc >= retention;
    }

    /// <summary>
    /// Descarta definitivamente el activo: queda rechazado y sin URL pública (FR-019-03).
    /// </summary>
    public void Purge()
    {
        Status = MediaAssetStatus.Rejected;
        SecureUrl = null;
    }
}

public enum MediaAssetStatus
{
    Pending = 0,
    Approved = 1,
    Quarantined = 2,
    Rejected = 3
}
