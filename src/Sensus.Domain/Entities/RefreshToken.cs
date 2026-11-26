using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Refresh token rotatorio perteneciente a una familia de sesión (Spec 002 / G-06).
/// Solo se persiste el hash SHA-256 del secreto; nunca el valor en claro.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? usedAtUtc = null,
        DateTime? revokedAtUtc = null,
        string? revocationReason = null,
        Guid? replacedByTokenId = null) : base(id)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        UsedAtUtc = usedAtUtc;
        RevokedAtUtc = revokedAtUtc;
        RevocationReason = revocationReason;
        ReplacedByTokenId = replacedByTokenId;
    }

    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive(DateTime nowUtc) =>
        UsedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public bool WasUsedOrRevoked => UsedAtUtc is not null || RevokedAtUtc is not null;

    public static Result<RefreshToken> Create(
        Guid userId,
        string tokenHash,
        DateTime nowUtc,
        DateTime expiresAtUtc,
        Guid? familyId = null,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<RefreshToken>(Error.Validation("RefreshToken.UserRequired", "El usuario es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result.Failure<RefreshToken>(Error.Validation("RefreshToken.HashRequired", "El hash del token es obligatorio."));
        }

        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        DateTime normalizedExpires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        if (normalizedExpires <= normalizedNow)
        {
            return Result.Failure<RefreshToken>(Error.Validation("RefreshToken.InvalidExpiry", "La expiración debe ser futura."));
        }

        Guid resolvedFamilyId = familyId.HasValue && familyId.Value != Guid.Empty ? familyId.Value : Guid.NewGuid();
        Guid resolvedId = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.NewGuid();

        return Result.Success(new RefreshToken(
            resolvedId,
            userId,
            resolvedFamilyId,
            tokenHash.Trim(),
            normalizedNow,
            normalizedExpires));
    }

    public static RefreshToken Reconstitute(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? usedAtUtc,
        DateTime? revokedAtUtc,
        string? revocationReason,
        Guid? replacedByTokenId) =>
        new(
            id,
            userId,
            familyId,
            tokenHash,
            DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc),
            usedAtUtc.HasValue ? DateTime.SpecifyKind(usedAtUtc.Value, DateTimeKind.Utc) : null,
            revokedAtUtc.HasValue ? DateTime.SpecifyKind(revokedAtUtc.Value, DateTimeKind.Utc) : null,
            revocationReason,
            replacedByTokenId);

    public bool TryMarkUsed(Guid replacedByTokenId, DateTime nowUtc)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (!IsActive(normalizedNow))
        {
            return false;
        }

        UsedAtUtc = normalizedNow;
        ReplacedByTokenId = replacedByTokenId;
        return true;
    }

    public void Revoke(DateTime nowUtc, string reason)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        RevocationReason = reason;
    }
}
