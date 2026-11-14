using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de dispositivo para notificaciones push en MongoDB Atlas (Spec 027).
/// </summary>
public sealed class DeviceDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("platform")]
    public string Platform { get; set; } = string.Empty;

    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("registeredAtUtc")]
    public DateTime RegisteredAtUtc { get; set; }

    [BsonElement("lastUsedAtUtc")]
    public DateTime? LastUsedAtUtc { get; set; }

    public static DeviceDocument FromDomain(Device device)
    {
        return new DeviceDocument
        {
            Id = device.Id,
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            Platform = device.Platform,
            Token = device.Token,
            IsActive = device.IsActive,
            RegisteredAtUtc = device.RegisteredAtUtc,
            LastUsedAtUtc = device.LastUsedAtUtc
        };
    }

    public Device ToDomain()
    {
        var device = Device.Create(
            UserId,
            DeviceId,
            Platform,
            Token,
            RegisteredAtUtc);

        // Restaurar estado persistido
        typeof(Device).GetProperty("Id")?.SetValue(device, Id);
        typeof(Device).GetProperty("IsActive")?.SetValue(device, IsActive);
        if (LastUsedAtUtc.HasValue)
        {
            typeof(Device).GetProperty("LastUsedAtUtc")?.SetValue(device, LastUsedAtUtc.Value);
        }

        return device;
    }
}
