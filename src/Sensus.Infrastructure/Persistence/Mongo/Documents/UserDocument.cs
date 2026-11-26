using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de usuario en MongoDB Atlas migrado desde el modelo SQL (Usuarios, Roles, UsuarioRoles).
/// </summary>
public sealed class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("nombreCompleto")]
    public string NombreCompleto { get; set; } = string.Empty;

    [BsonElement("correo")]
    public string Correo { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("securityStamp")]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("fotoPerfilUrl")]
    public string? FotoPerfilUrl { get; set; }

    [BsonElement("aceptoTerminos")]
    public bool AceptoTerminos { get; set; }

    [BsonElement("fechaAceptacionTerminos")]
    public DateTime? FechaAceptacionTerminos { get; set; }

    [BsonElement("estatus")]
    public bool Estatus { get; set; }

    [BsonElement("fechaRegistro")]
    public DateTime FechaRegistro { get; set; }

    [BsonElement("ultimoAcceso")]
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Marca de baja lógica (Spec 060). Nula para cuentas activas.</summary>
    [BsonElement("deletedAtUtc")]
    public DateTime? DeletedAtUtc { get; set; }

    [BsonElement("roles")]
    public List<string> Roles { get; set; } = [];

    /// <summary>Sanción que mantiene suspendido al usuario (Spec 020).</summary>
    [BsonElement("suspensionSanctionId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? SuspensionSanctionId { get; set; }

    /// <summary>Fin de la suspensión; nulo con <see cref="SuspensionSanctionId"/> presente indica baneo permanente.</summary>
    [BsonElement("suspensionEndsAtUtc")]
    public DateTime? SuspensionEndsAtUtc { get; set; }

    /// <summary>Estado de segundo factor TOTP (Spec 062).</summary>
    [BsonElement("mfa")]
    public UserMfaDocument? Mfa { get; set; }

    public static UserDocument FromDomain(User user)
    {
        return new UserDocument
        {
            Id = user.Id,
            NombreCompleto = user.NombreCompleto,
            Correo = user.Correo,
            PasswordHash = user.PasswordHash,
            SecurityStamp = user.SecurityStamp,
            FotoPerfilUrl = user.FotoPerfilUrl,
            AceptoTerminos = user.AceptoTerminos,
            FechaAceptacionTerminos = user.FechaAceptacionTerminos,
            Estatus = user.Estatus,
            FechaRegistro = user.FechaRegistro,
            UltimoAcceso = user.UltimoAcceso,
            DeletedAtUtc = user.DeletedAtUtc,
            Roles = user.Roles.ToList(),
            SuspensionSanctionId = user.Suspension?.SanctionId,
            SuspensionEndsAtUtc = user.Suspension?.EndsAtUtc,
            Mfa = user.Mfa is null
                ? null
                : new UserMfaDocument
                {
                    IsEnabled = user.Mfa.IsEnabled,
                    IsPendingConfirmation = user.Mfa.IsPendingConfirmation,
                    EncryptedSecret = user.Mfa.EncryptedSecret,
                    RecoveryCodeHashes = user.Mfa.RecoveryCodeHashes.ToList(),
                    FailedAttempts = user.Mfa.FailedAttempts,
                    LockedUntilUtc = user.Mfa.LockedUntilUtc,
                    EnrolledAtUtc = user.Mfa.EnrolledAtUtc,
                    LastVerifiedTimeStep = user.Mfa.LastVerifiedTimeStep
                }
        };
    }

    public User ToDomain()
    {
        var user = User.Create(
            NombreCompleto,
            Correo,
            PasswordHash,
            AceptoTerminos,
            FechaRegistro,
            string.IsNullOrWhiteSpace(SecurityStamp) ? Guid.NewGuid().ToString("N") : SecurityStamp).Value;

        // Establecer el Id persistido y estado
        typeof(User).GetProperty("Id")?.SetValue(user, Id);

        if (!string.IsNullOrWhiteSpace(FotoPerfilUrl))
        {
            user.UpdateProfile(NombreCompleto, FotoPerfilUrl);
        }

        if (DeletedAtUtc is { } deletedAt)
        {
            user.RestoreDeletion(deletedAt);
        }
        else if (!Estatus)
        {
            user.Deactivate();
        }

        if (UltimoAcceso.HasValue)
        {
            user.RecordAccess(UltimoAcceso.Value);
        }

        // Agregar roles adicionales del modelo unificado si existen
        foreach (var role in Roles)
        {
            if (!user.HasRole(role))
            {
                user.AddRole(role);
            }
        }

        if (SuspensionSanctionId is { } sanctionId)
        {
            user.RestoreSuspension(
                sanctionId,
                SuspensionEndsAtUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null);
        }

        if (Mfa is not null)
        {
            user.RestoreMfa(new UserMfaState(
                Mfa.IsEnabled,
                Mfa.IsPendingConfirmation,
                Mfa.EncryptedSecret,
                Mfa.RecoveryCodeHashes,
                Mfa.FailedAttempts,
                Mfa.LockedUntilUtc is { } locked ? DateTime.SpecifyKind(locked, DateTimeKind.Utc) : null,
                Mfa.EnrolledAtUtc is { } enrolled ? DateTime.SpecifyKind(enrolled, DateTimeKind.Utc) : null,
                Mfa.LastVerifiedTimeStep));
        }

        user.ClearDomainEvents();
        return user;
    }
}

public sealed class UserMfaDocument
{
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; }

    [BsonElement("isPendingConfirmation")]
    public bool IsPendingConfirmation { get; set; }

    [BsonElement("encryptedSecret")]
    public string EncryptedSecret { get; set; } = string.Empty;

    [BsonElement("recoveryCodeHashes")]
    public List<string> RecoveryCodeHashes { get; set; } = [];

    [BsonElement("failedAttempts")]
    public int FailedAttempts { get; set; }

    [BsonElement("lockedUntilUtc")]
    public DateTime? LockedUntilUtc { get; set; }

    [BsonElement("enrolledAtUtc")]
    public DateTime? EnrolledAtUtc { get; set; }

    [BsonElement("lastVerifiedTimeStep")]
    public long? LastVerifiedTimeStep { get; set; }
}
