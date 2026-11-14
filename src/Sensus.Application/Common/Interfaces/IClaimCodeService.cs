namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Resultado de generar y firmar el código de un apartado (FR-033-04).
/// </summary>
public sealed record ClaimCode(string Code, string Hash, string QrPayload);

/// <summary>
/// Contenido firmado del código de un apartado.
/// </summary>
public sealed record ClaimCodePayload(Guid ClaimId, Guid OfferId, DateTime ExpiresAtUtc);

/// <summary>
/// Contrato para generar, firmar y validar los códigos de apartado con HMAC (FR-033-04, FR-033-08).
/// </summary>
public interface IClaimCodeService
{
    /// <summary>Genera el código firmado, su hash y el payload que el cliente renderiza como QR.</summary>
    ClaimCode Generate(Guid claimId, Guid offerId, DateTime expiresAtUtc);

    /// <summary>Calcula el hash que se persiste; el código en claro nunca se almacena.</summary>
    string ComputeHash(string code);

    /// <summary>Valida firma y vigencia del código recibido.</summary>
    bool TryValidate(string code, DateTime nowUtc, out ClaimCodePayload payload);
}
