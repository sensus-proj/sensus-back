namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Resultado de seudonimizar un identificador de usuario con clave HMAC versionada (FR-055-04).
/// </summary>
public readonly record struct PseudonymizedUser(string PseudoUserId, int KeyVersion);

/// <summary>
/// Seudonimiza el identificador de usuario mediante HMAC-SHA256 con clave versionada en SSM (Spec 055).
/// </summary>
public interface IUserPseudonymizer
{
    /// <summary>Calcula el seudónimo con la versión activa de clave.</summary>
    PseudonymizedUser Pseudonymize(Guid userId);

    /// <summary>Calcula los seudónimos para todas las versiones de clave vigentes (purga FR-055-07).</summary>
    IReadOnlyList<PseudonymizedUser> ComputeAllKnownPseudonyms(Guid userId);
}
