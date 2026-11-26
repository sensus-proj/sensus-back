namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración para el segundo factor TOTP y step-up de Administrador (Spec 062).
/// </summary>
public sealed class MfaSettings
{
    public const string SectionName = "Mfa";

    /// <summary>
    /// Clave simétrica de 256 bits en Base64 para cifrado AES-GCM del secreto TOTP en reposo (SSM /sensus/&lt;env&gt;/Mfa/EncryptionKey).
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Emisor mostrado en aplicaciones autenticadoras.</summary>
    public string Issuer { get; set; } = "Sensus";

    /// <summary>Ventana máxima de recencia (minutos) del claim mfa_at para acciones AdminOnly.</summary>
    public int StepUpWindowMinutes { get; set; } = 15;

    /// <summary>Vigencia (minutos) del token intermedio de desafío emitido en el login.</summary>
    public int ChallengeExpiryMinutes { get; set; } = 5;

    /// <summary>Intentos fallidos consecutivos antes de bloquear temporalmente la verificación MFA.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Duración del bloqueo temporal en minutos tras agotar intentos fallidos.</summary>
    public int LockoutMinutes { get; set; } = 15;
}
