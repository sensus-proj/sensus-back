using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;

namespace Sensus.Infrastructure.Authentication;

/// <summary>
/// Implementación nativa .NET de TOTP RFC 6238 (HMACSHA1), cifrado AES-256-GCM en reposo y códigos de recuperación (Spec 062).
/// Sin dependencias externas de terceros.
/// </summary>
public sealed class Rfc6238TotpService : ITotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TimeStepSeconds = 30;
    private const int AesNonceSizeBytes = 12;
    private const int AesTagSizeBytes = 16;
    private const int RequiredKeySizeBytes = 32;

    private readonly MfaSettings _settings;

    public Rfc6238TotpService(IOptions<MfaSettings> options)
    {
        _settings = options.Value;
    }

    public string GenerateSecret(int byteLength = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteLength, 16);
        byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base32Encode(bytes);
    }

    public string BuildOtpAuthUri(string issuer, string accountEmail, string base32Secret)
    {
        string normalizedIssuer = string.IsNullOrWhiteSpace(issuer) ? "Sensus" : issuer.Trim();
        string label = Uri.EscapeDataString($"{normalizedIssuer}:{accountEmail.Trim()}");
        string escapedIssuer = Uri.EscapeDataString(normalizedIssuer);
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={escapedIssuer}&algorithm=SHA1&digits=6&period={TimeStepSeconds}";
    }

    public string ComputeTotp(string base32Secret, DateTime utcTimestamp, int digits = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 6);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 10);

        byte[] key = Base32Decode(base32Secret);
        long timeStep = GetTimeStep(utcTimestamp);
        return ComputeTotpForStep(key, timeStep, digits);
    }

    public bool TryVerifyCode(
        string base32Secret,
        string code,
        DateTime utcTimestamp,
        out long matchedTimeStep,
        int allowedStepDrift = 1)
    {
        matchedTimeStep = 0;
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmed = code.Trim();
        if (trimmed.Length != 6 || !trimmed.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32Decode(base32Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        long currentStep = GetTimeStep(utcTimestamp);
        byte[] candidateBytes = Encoding.ASCII.GetBytes(trimmed);

        for (int drift = -allowedStepDrift; drift <= allowedStepDrift; drift++)
        {
            long candidateStep = currentStep + drift;
            if (candidateStep < 0)
            {
                continue;
            }

            string expected = ComputeTotpForStep(key, candidateStep, 6);
            byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
            if (CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes))
            {
                matchedTimeStep = candidateStep;
                return true;
            }
        }

        return false;
    }

    public Result<string> EncryptSecret(string base32Secret)
    {
        if (string.IsNullOrWhiteSpace(base32Secret))
        {
            return Result.Failure<string>(Error.Failure("Auth.MfaInvalidSecret", "El secreto TOTP es obligatorio."));
        }

        Result<byte[]> keyResult = ResolveEncryptionKey();
        if (keyResult.IsFailure)
        {
            return Result.Failure<string>(keyResult.Error);
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(base32Secret);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesNonceSizeBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AesTagSizeBytes];

        using var aes = new AesGcm(keyResult.Value, AesTagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Result.Success($"v1:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(tag)}");
    }

    public Result<string> DecryptSecret(string encryptedPayload)
    {
        if (string.IsNullOrWhiteSpace(encryptedPayload))
        {
            return Result.Failure<string>(Error.Failure("Auth.MfaSecretMissing", "No hay secreto TOTP cifrado configurado."));
        }

        Result<byte[]> keyResult = ResolveEncryptionKey();
        if (keyResult.IsFailure)
        {
            return Result.Failure<string>(keyResult.Error);
        }

        string[] parts = encryptedPayload.Split(':');
        if (parts.Length != 4 || !string.Equals(parts[0], "v1", StringComparison.Ordinal))
        {
            return Result.Failure<string>(Error.Failure("Auth.MfaSecretCorrupted", "Formato de secreto TOTP cifrado inválido."));
        }

        try
        {
            byte[] nonce = Convert.FromBase64String(parts[1]);
            byte[] ciphertext = Convert.FromBase64String(parts[2]);
            byte[] tag = Convert.FromBase64String(parts[3]);

            if (nonce.Length != AesNonceSizeBytes || tag.Length != AesTagSizeBytes)
            {
                return Result.Failure<string>(Error.Failure("Auth.MfaSecretCorrupted", "Metadatos de cifrado TOTP inválidos."));
            }

            byte[] plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(keyResult.Value, AesTagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Result.Success(Encoding.UTF8.GetString(plaintext));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return Result.Failure<string>(Error.Failure("Auth.MfaSecretCorrupted", "No se pudo descifrar el secreto TOTP."));
        }
    }

    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var codes = new HashSet<string>(count, StringComparer.Ordinal);
        while (codes.Count < count)
        {
            var chars = new char[9];
            for (int i = 0; i < 9; i++)
            {
                chars[i] = i == 4
                    ? '-'
                    : RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
            }

            codes.Add(new string(chars));
        }

        return codes.ToArray();
    }

    public string HashRecoveryCode(string recoveryCode)
    {
        string normalized = NormalizeRecoveryCode(recoveryCode);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }

    public bool VerifyRecoveryCode(string candidateCode, IReadOnlyList<string> storedHashes, out string? matchedHash)
    {
        matchedHash = null;
        if (string.IsNullOrWhiteSpace(candidateCode) || storedHashes.Count == 0)
        {
            return false;
        }

        string candidateHash = HashRecoveryCode(candidateCode);
        byte[] candidateBytes = Encoding.ASCII.GetBytes(candidateHash);

        foreach (string stored in storedHashes)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                continue;
            }

            byte[] storedBytes = Encoding.ASCII.GetBytes(stored.Trim().ToLowerInvariant());
            if (candidateBytes.Length == storedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(candidateBytes, storedBytes))
            {
                matchedHash = stored;
                return true;
            }
        }

        return false;
    }

    private Result<byte[]> ResolveEncryptionKey()
    {
        if (string.IsNullOrWhiteSpace(_settings.EncryptionKey))
        {
            return Result.Failure<byte[]>(Error.Failure(
                "Auth.MfaKeyMissing",
                "La clave de cifrado MFA (Mfa:EncryptionKey) no está configurada."));
        }

        try
        {
            byte[] key = Convert.FromBase64String(_settings.EncryptionKey.Trim());
            if (key.Length != RequiredKeySizeBytes)
            {
                return Result.Failure<byte[]>(Error.Failure(
                    "Auth.MfaKeyInvalid",
                    "Mfa:EncryptionKey debe ser una clave de 256 bits (32 bytes) en Base64."));
            }

            return Result.Success(key);
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(Error.Failure(
                "Auth.MfaKeyInvalid",
                "Mfa:EncryptionKey no tiene formato Base64 válido."));
        }
    }

    private static long GetTimeStep(DateTime utcTimestamp)
    {
        DateTime utc = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        long unixSeconds = new DateTimeOffset(utc).ToUnixTimeSeconds();
        return unixSeconds >= 0
            ? unixSeconds / TimeStepSeconds
            : (unixSeconds - TimeStepSeconds + 1) / TimeStepSeconds;
    }

    private static string ComputeTotpForStep(byte[] key, long timeStep, int digits)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, timeStep);

        Span<byte> hash = stackalloc byte[20];
        // Justificación RFC 6238 (§4): TOTP estándar compatible con apps autenticadoras usa HMAC-SHA-1 (PRF).
#pragma warning disable CA5350
        HMACSHA1.HashData(key, counterBytes, hash);
#pragma warning restore CA5350

        int offset = hash[19] & 0x0F;
        int binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        long modulus = 1;
        for (int i = 0; i < digits; i++)
        {
            modulus *= 10;
        }

        long otp = binaryCode % modulus;
        return otp.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    private static string NormalizeRecoveryCode(string code) =>
        new string(code.Trim().Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        string normalized = base32.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return [];
        }

        var bytes = new List<byte>(normalized.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in normalized)
        {
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException($"Carácter Base32 inválido: '{c}'.");
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return [.. bytes];
    }
}
