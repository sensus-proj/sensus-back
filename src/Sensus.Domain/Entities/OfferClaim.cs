using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Raíz de agregado que representa el apartado de una unidad de una oferta con un código de un
/// solo uso (FR-033-01 a FR-033-08).
/// </summary>
public class OfferClaim : BaseAuditableEntity, IAggregateRoot
{
    /// <summary>Vigencia por defecto del apartado (FR-033-05).</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);

    public Guid UserId { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid MerchantId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public OfferClaimStatus Status { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RedeemedAtUtc { get; private set; }

    private OfferClaim() : base()
    {
    }

    private OfferClaim(
        Guid id,
        Guid userId,
        Guid offerId,
        Guid merchantId,
        string codeHash,
        OfferClaimStatus status,
        DateTime expiresAtUtc,
        DateTime? redeemedAtUtc,
        DateTime createdAtUtc) : base(id)
    {
        UserId = userId;
        OfferId = offerId;
        MerchantId = merchantId;
        CodeHash = codeHash;
        Status = status;
        ExpiresAtUtc = expiresAtUtc;
        RedeemedAtUtc = redeemedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Crea un apartado activo. El hash del código se asigna después con <see cref="AssignCodeHash"/>.
    /// </summary>
    public static Result<OfferClaim> Create(
        Guid userId,
        Guid offerId,
        Guid merchantId,
        DateTime nowUtc,
        TimeSpan? lifetime = null)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<OfferClaim>(
                Error.Validation("OfferClaim.UserRequired", "El identificador del usuario es obligatorio."));
        }

        if (offerId == Guid.Empty)
        {
            return Result.Failure<OfferClaim>(
                Error.Validation("OfferClaim.OfferRequired", "El identificador de la oferta es obligatorio."));
        }

        if (merchantId == Guid.Empty)
        {
            return Result.Failure<OfferClaim>(
                Error.Validation("OfferClaim.MerchantRequired", "El identificador del comercio es obligatorio."));
        }

        TimeSpan effectiveLifetime = lifetime ?? DefaultLifetime;
        if (effectiveLifetime <= TimeSpan.Zero)
        {
            return Result.Failure<OfferClaim>(
                Error.Validation("OfferClaim.InvalidLifetime", "La vigencia del apartado debe ser mayor a 0."));
        }

        DateTime createdAt = nowUtc.ToUniversalTime();

        var claim = new OfferClaim(
            Guid.NewGuid(),
            userId,
            offerId,
            merchantId,
            string.Empty,
            OfferClaimStatus.Active,
            createdAt.Add(effectiveLifetime),
            null,
            createdAt);

        return Result.Success(claim);
    }

    /// <summary>
    /// Reconstituye un apartado desde persistencia.
    /// </summary>
    public static OfferClaim Reconstitute(
        Guid id,
        Guid userId,
        Guid offerId,
        Guid merchantId,
        string codeHash,
        OfferClaimStatus status,
        DateTime expiresAtUtc,
        DateTime? redeemedAtUtc,
        DateTime createdAtUtc)
    {
        return new OfferClaim(
            id,
            userId,
            offerId,
            merchantId,
            codeHash,
            status,
            expiresAtUtc,
            redeemedAtUtc,
            createdAtUtc);
    }

    /// <summary>
    /// Asigna el hash del código firmado. Solo puede asignarse una vez.
    /// </summary>
    public Result AssignCodeHash(string codeHash)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            return Result.Failure(
                Error.Validation("OfferClaim.CodeHashRequired", "El hash del código es obligatorio."));
        }

        if (!string.IsNullOrEmpty(CodeHash))
        {
            return Result.Failure(
                Error.Conflict("OfferClaim.CodeAlreadyAssigned", "El apartado ya tiene un código asignado."));
        }

        CodeHash = codeHash.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Marca el apartado como canjeado validando propiedad y vigencia.
    /// </summary>
    public Result Redeem(Guid merchantId, DateTime nowUtc)
    {
        if (Status == OfferClaimStatus.Redeemed)
        {
            return Result.Failure(
                Error.Conflict("OfferClaim.AlreadyRedeemed", "El apartado ya fue canjeado."));
        }

        if (Status == OfferClaimStatus.Expired)
        {
            return Result.Failure(
                Error.Conflict("OfferClaim.AlreadyExpired", "El apartado ya expiró."));
        }

        if (merchantId != MerchantId)
        {
            return Result.Failure(
                Error.Forbidden("OfferClaim.NotOwner", "El código pertenece a otro comercio."));
        }

        if (nowUtc >= ExpiresAtUtc)
        {
            Status = OfferClaimStatus.Expired;
            return Result.Failure(
                Error.Conflict("OfferClaim.Expired", "El apartado expiró y el código ya no es válido."));
        }

        Status = OfferClaimStatus.Redeemed;
        RedeemedAtUtc = nowUtc.ToUniversalTime();
        return Result.Success();
    }

    /// <summary>
    /// Expira el apartado si ya superó su fecha de vigencia.
    /// </summary>
    public Result Expire(DateTime nowUtc)
    {
        if (Status != OfferClaimStatus.Active)
        {
            return Result.Failure(
                Error.Conflict("OfferClaim.NotActive", "Solo se pueden expirar apartados activos."));
        }

        if (nowUtc < ExpiresAtUtc)
        {
            return Result.Failure(
                Error.Conflict("OfferClaim.NotYetExpired", "El apartado aún no ha alcanzado su fecha de expiración."));
        }

        Status = OfferClaimStatus.Expired;
        return Result.Success();
    }

    public bool IsActive(DateTime nowUtc) =>
        Status == OfferClaimStatus.Active && nowUtc < ExpiresAtUtc;
}
