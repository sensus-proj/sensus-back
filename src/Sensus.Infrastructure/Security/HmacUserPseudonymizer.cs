using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;

namespace Sensus.Infrastructure.Security;

/// <summary>
/// Seudonimiza identificadores de usuario con HMAC-SHA256 usando claves Base64 versionadas
/// cargadas desde SSM Parameter Store (<c>/sensus/&lt;env&gt;/Interactions/...</c>, Spec 055).
/// </summary>
public sealed class HmacUserPseudonymizer : IUserPseudonymizer
{
    public const int MinimumKeyBytes = 32;

    private readonly int _activeVersion;
    private readonly Dictionary<int, byte[]> _keys;

    public HmacUserPseudonymizer(IOptions<InteractionSettings> settings)
    {
        InteractionSettings opts = settings.Value;
        _activeVersion = opts.ActiveKeyVersion > 0 ? opts.ActiveKeyVersion : 1;
        _keys = ParseKeys(opts);
    }

    public PseudonymizedUser Pseudonymize(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        }

        if (!_keys.TryGetValue(_activeVersion, out byte[]? keyBytes))
        {
            throw new InvalidOperationException(
                $"No se encontró una clave HMAC válida para la versión activa {_activeVersion}.");
        }

        string digest = ComputeHexHmac(userId, keyBytes);
        return new PseudonymizedUser(digest, _activeVersion);
    }

    public IReadOnlyList<PseudonymizedUser> ComputeAllKnownPseudonyms(Guid userId)
    {
        if (userId == Guid.Empty || _keys.Count == 0)
        {
            return Array.Empty<PseudonymizedUser>();
        }

        var result = new List<PseudonymizedUser>(_keys.Count);
        foreach (KeyValuePair<int, byte[]> pair in _keys.OrderBy(k => k.Key))
        {
            result.Add(new PseudonymizedUser(ComputeHexHmac(userId, pair.Value), pair.Key));
        }

        return result;
    }

    private static string ComputeHexHmac(Guid userId, byte[] keyBytes)
    {
        byte[] payload = Encoding.UTF8.GetBytes(userId.ToString("N"));
        byte[] hash = HMACSHA256.HashData(keyBytes, payload);
        return Convert.ToHexStringLower(hash);
    }

    private static Dictionary<int, byte[]> ParseKeys(InteractionSettings settings)
    {
        var parsed = new Dictionary<int, byte[]>();

        foreach (KeyValuePair<int, string> entry in settings.HmacKeys)
        {
            if (entry.Key >= 1 && TryDecodeKey(entry.Value, out byte[] bytes))
            {
                parsed[entry.Key] = bytes;
            }
        }

        if (parsed.Count == 0 && !string.IsNullOrWhiteSpace(settings.HmacKeysJson))
        {
            try
            {
                Dictionary<string, string>? jsonMap = JsonSerializer.Deserialize<Dictionary<string, string>>(settings.HmacKeysJson);
                if (jsonMap is not null)
                {
                    foreach (KeyValuePair<string, string> entry in jsonMap)
                    {
                        if (int.TryParse(entry.Key, out int version) && version >= 1 && TryDecodeKey(entry.Value, out byte[] bytes))
                        {
                            parsed[version] = bytes;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Configuración malformada falla cerrado en Pseudonymize sin exponer secretos.
            }
        }

        return parsed;
    }

    private static bool TryDecodeKey(string? rawBase64, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(rawBase64))
        {
            return false;
        }

        try
        {
            byte[] decoded = Convert.FromBase64String(rawBase64.Trim());
            if (decoded.Length < MinimumKeyBytes)
            {
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
