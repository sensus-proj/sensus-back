using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de repositorio para la entidad de dominio User.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca la cuenta como dada de baja de forma condicional (Spec 060, FR-060-01 y FR-060-02).
    /// Devuelve <c>true</c> solo si la cuenta estaba activa y la transición se aplicó.
    /// </summary>
    Task<bool> MarkAsDeletedAsync(
        Guid id,
        DateTime deletedAtUtc,
        string securityStamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los identificadores de cuentas dadas de baja antes del <c>cutoff</c> (FR-060-04).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetIdsDeletedBeforeAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una cuenta solo si sigue dada de baja antes del <c>cutoff</c> (FR-060-04, FR-060-06).
    /// Devuelve <c>true</c> solo si se eliminó un documento, para evitar auditoría duplicada.
    /// </summary>
    Task<bool> PurgeDeletedAsync(
        Guid id,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}
