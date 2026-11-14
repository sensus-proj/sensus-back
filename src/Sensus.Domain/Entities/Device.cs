using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa un dispositivo registrado para notificaciones push (Spec 027).
/// </summary>
public sealed class Device : BaseEntity
{
    private Device(
        Guid id,
        Guid userId,
        string deviceId,
        string platform,
        string token,
        bool isActive,
        DateTime registeredAtUtc)
    {
        Id = id;
        UserId = userId;
        DeviceId = deviceId;
        Platform = platform;
        Token = token;
        IsActive = isActive;
        RegisteredAtUtc = registeredAtUtc;
    }

    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }

    public static Device Create(
        Guid userId,
        string deviceId,
        string platform,
        string token,
        DateTime registeredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("El deviceId es obligatorio.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(platform))
            throw new ArgumentException("La plataforma es obligatoria.", nameof(platform));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("El token de push es obligatorio.", nameof(token));

        return new Device(
            Guid.NewGuid(),
            userId,
            deviceId.Trim(),
            platform.Trim(),
            token.Trim(),
            true,
            registeredAtUtc);
    }

    public void UpdateToken(string newToken)
    {
        if (string.IsNullOrWhiteSpace(newToken))
            throw new ArgumentException("El nuevo token no puede estar vacío.", nameof(newToken));
        Token = newToken.Trim();
        LastUsedAtUtc = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
