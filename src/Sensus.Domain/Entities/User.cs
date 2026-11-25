using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Events;

namespace Sensus.Domain.Entities;

/// <summary>
/// Raíz de Agregado Usuario que implementa el Modelo Unificado (Airbnb) para Consumidor y Comerciante.
/// </summary>
public class User : BaseEntity, IAggregateRoot
{
    private readonly List<string> _roles = [];

    public string NombreCompleto { get; private set; } = string.Empty;
    public string Correo { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString("N");
    public string? FotoPerfilUrl { get; private set; }
    public bool AceptoTerminos { get; private set; }
    public DateTime? FechaAceptacionTerminos { get; private set; }
    public bool Estatus { get; private set; }
    public DateTime FechaRegistro { get; private set; }
    public DateTime? UltimoAcceso { get; private set; }

    /// <summary>
    /// Marca de baja lógica de la cuenta (Spec 060, FR-060-01). Nula mientras la cuenta está activa.
    /// La PII se conserva hasta que el proceso de purga elimina el documento.
    /// </summary>
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Indica si la cuenta fue dada de baja lógicamente (Spec 060).</summary>
    public bool IsDeleted => DeletedAtUtc is not null;

    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();

    /// <summary>
    /// Suspensión vigente por sanción (Spec 020). Se denormaliza en el usuario para bloquear sin consultas extra.
    /// </summary>
    public UserSuspension? Suspension { get; private set; }

    // Constructor privado para serialización / ORM
    private User() : base()
    {
    }

    private User(
        Guid id,
        string nombreCompleto,
        string correo,
        string passwordHash,
        bool aceptoTerminos,
        DateTime fechaRegistro,
        string? securityStamp = null) : base(id)
    {
        NombreCompleto = nombreCompleto;
        Correo = correo;
        PasswordHash = passwordHash;
        SecurityStamp = string.IsNullOrWhiteSpace(securityStamp) ? Guid.NewGuid().ToString("N") : securityStamp;
        AceptoTerminos = aceptoTerminos;
        FechaAceptacionTerminos = aceptoTerminos ? fechaRegistro : null;
        Estatus = true; // Activo por defecto
        FechaRegistro = fechaRegistro;
        _roles.Add(RoleType.Consumidor); // Rol unificado base
    }

    /// <summary>
    /// Factory method para registrar un nuevo usuario en el sistema.
    /// </summary>
    public static Result<User> Create(
        string nombreCompleto,
        string correo,
        string passwordHash,
        bool aceptoTerminos,
        DateTime fechaRegistro,
        string? securityStamp = null)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return Result.Failure<User>(
                Error.Validation("User.NombreVacio", "El nombre completo es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            return Result.Failure<User>(
                Error.Validation("User.CorreoVacio", "El correo electrónico es obligatorio."));
        }

        if (!aceptoTerminos)
        {
            return Result.Failure<User>(
                Error.Validation("User.TerminosRequeridos", "Debe aceptar los términos y condiciones para registrarse."));
        }

        var normalizedEmail = correo.Trim().ToLowerInvariant();
        var user = new User(
            Guid.NewGuid(),
            nombreCompleto.Trim(),
            normalizedEmail,
            passwordHash,
            aceptoTerminos,
            fechaRegistro,
            securityStamp);

        user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Correo, user.NombreCompleto));

        return Result.Success(user);
    }

    /// <summary>
    /// Agrega un rol al usuario para el modelo unificado (ej. Comerciante en SE-003).
    /// </summary>
    public Result AddRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Result.Failure(Error.Validation("Role.Empty", "El nombre del rol no puede estar vacío."));
        }

        if (!_roles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            _roles.Add(role);
        }

        return Result.Success();
    }

    /// <summary>
    /// Comprueba si el usuario tiene asignado un rol específico.
    /// </summary>
    public bool HasRole(string role) =>
        _roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registra la fecha y hora del último acceso exitoso del usuario.
    /// </summary>
    public void RecordAccess(DateTime timestampUtc)
    {
        UltimoAcceso = timestampUtc;
        AddDomainEvent(new UserLoggedInDomainEvent(Id, timestampUtc));
    }

    /// <summary>
    /// Actualiza el hash de contraseña en caso de recuperación o cambio e invalida tokens previos.
    /// </summary>
    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RotateSecurityStamp();
    }

    /// <summary>
    /// Rota el SecurityStamp para invalidar sesiones activas.
    /// </summary>
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Actualiza la información personal del perfil (SE-006).
    /// </summary>
    public Result UpdateProfile(string nombreCompleto, string? fotoPerfilUrl)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return Result.Failure(Error.Validation("User.NombreVacio", "El nombre completo no puede estar vacío."));
        }

        NombreCompleto = nombreCompleto.Trim();
        if (fotoPerfilUrl is not null)
        {
            FotoPerfilUrl = fotoPerfilUrl.Trim();
        }

        return Result.Success();
    }

    /// <summary>
    /// Da de baja la cuenta lógicamente, conservando la PII hasta la purga (Spec 060, FR-060-01).
    /// Desactiva la cuenta y rota el SecurityStamp para revocar de inmediato los tokens emitidos
    /// (FR-060-02). Devuelve <c>false</c> si la cuenta ya estaba dada de baja (idempotencia).
    /// </summary>
    public bool MarkAsDeleted(DateTime deletedAtUtc)
    {
        if (IsDeleted)
        {
            return false;
        }

        DeletedAtUtc = DateTime.SpecifyKind(deletedAtUtc, DateTimeKind.Utc);
        Deactivate();
        return true;
    }

    /// <summary>
    /// Restaura la marca de baja persistida. Uso exclusivo de la capa de persistencia: no rota el stamp.
    /// </summary>
    public void RestoreDeletion(DateTime deletedAtUtc)
    {
        DeletedAtUtc = DateTime.SpecifyKind(deletedAtUtc, DateTimeKind.Utc);
        Estatus = false;
    }

    /// <summary>
    /// Desactiva la cuenta del usuario e invalida sesiones activas.
    /// </summary>
    public void Deactivate()
    {
        Estatus = false;
        RotateSecurityStamp();
    }

    /// <summary>
    /// Activa la cuenta del usuario.
    /// </summary>
    public void Activate()
    {
        Estatus = true;
    }

    /// <summary>
    /// Indica si el usuario está bloqueado por una sanción en el instante indicado (Spec 020, FR-020-04).
    /// Una suspensión con fecha de fin vencida deja de bloquear sin intervención (CA-020-06).
    /// </summary>
    public bool IsSuspended(DateTime nowUtc) =>
        Suspension is not null && (Suspension.EndsAtUtc is null || Suspension.EndsAtUtc > nowUtc);

    /// <summary>
    /// Suspende al usuario e invalida sus sesiones. Si ya existe una suspensión vigente más severa se conserva.
    /// </summary>
    public void Suspend(Guid sanctionId, DateTime? endsAtUtc, DateTime nowUtc)
    {
        var candidate = new UserSuspension(sanctionId, endsAtUtc);
        if (!IsSuspended(nowUtc) || candidate.IsAtLeastAsSevereAs(Suspension!))
        {
            Suspension = candidate;
        }

        RotateSecurityStamp();
    }

    /// <summary>
    /// Levanta la suspensión sin tocar datos del usuario (NFR-020-02). Solo aplica si corresponde a la sanción indicada.
    /// </summary>
    public bool LiftSuspension(Guid sanctionId)
    {
        if (Suspension is null || Suspension.SanctionId != sanctionId)
        {
            return false;
        }

        Suspension = null;
        return true;
    }

    /// <summary>
    /// Restaura una suspensión persistida. Uso exclusivo de la capa de persistencia: no rota el stamp.
    /// </summary>
    public void RestoreSuspension(Guid sanctionId, DateTime? endsAtUtc)
    {
        Suspension = new UserSuspension(sanctionId, endsAtUtc);
    }
}

/// <summary>
/// Suspensión vigente de un usuario; <see cref="EndsAtUtc"/> nulo indica baneo permanente.
/// </summary>
public sealed record UserSuspension(Guid SanctionId, DateTime? EndsAtUtc)
{
    public bool IsAtLeastAsSevereAs(UserSuspension other) =>
        EndsAtUtc is null || (other.EndsAtUtc is not null && EndsAtUtc >= other.EndsAtUtc);
}
