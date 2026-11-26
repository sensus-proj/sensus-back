using Sensus.Domain.Common;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Servicio criptográfico TOTP RFC 6238, cifrado AES-GCM del secreto y códigos de recuperación (Spec 062).
/// </summary>
public interface ITotpService
{
    string GenerateSecret(int byteLength = 20);
    string BuildOtpAuthUri(string issuer, string accountEmail, string base32Secret);
    string ComputeTotp(string base32Secret, DateTime utcTimestamp, int digits = 6);
    bool TryVerifyCode(string base32Secret, string code, DateTime utcTimestamp, out long matchedTimeStep, int allowedStepDrift = 1);
    Result<string> EncryptSecret(string base32Secret);
    Result<string> DecryptSecret(string encryptedPayload);
    IReadOnlyList<string> GenerateRecoveryCodes(int count = 8);
    string HashRecoveryCode(string recoveryCode);
    bool VerifyRecoveryCode(string candidateCode, IReadOnlyList<string> storedHashes, out string? matchedHash);
}
