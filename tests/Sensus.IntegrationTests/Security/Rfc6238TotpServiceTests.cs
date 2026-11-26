using FluentAssertions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;
using Sensus.Infrastructure.Authentication;

namespace Sensus.IntegrationTests.Security;

public sealed class Rfc6238TotpServiceTests
{
    // Secreto ASCII estándar de RFC 6238 Apéndice B ("12345678901234567890") codificado en Base32 RFC 4648.
    private const string Rfc6238Sha1Base32Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private static Rfc6238TotpService CreateService(string? encryptionKeyBase64 = null)
    {
        var key = encryptionKeyBase64 ?? Convert.ToBase64String(new byte[32]);
        return new Rfc6238TotpService(Options.Create(new MfaSettings
        {
            EncryptionKey = key,
            Issuer = "Sensus"
        }));
    }

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void ComputeTotp_MatchesOfficialRfc6238Sha1TestVectors(long unixSeconds, string expected8DigitTotp)
    {
        var service = CreateService();
        var utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

        var totp = service.ComputeTotp(Rfc6238Sha1Base32Secret, utc, digits: 8);

        totp.Should().Be(expected8DigitTotp);
    }

    [Fact]
    public void TryVerifyCode_AllowsAdjacentTimeStepAndRejectsTwoStepsAway()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();
        var t0 = new DateTime(2026, 11, 26, 12, 0, 15, DateTimeKind.Utc);
        var code = service.ComputeTotp(secret, t0);

        service.TryVerifyCode(secret, code, t0, out var exactStep).Should().BeTrue();
        service.TryVerifyCode(secret, code, t0.AddSeconds(30), out var nextStep).Should().BeTrue();
        nextStep.Should().Be(exactStep);

        service.TryVerifyCode(secret, code, t0.AddSeconds(60), out _).Should().BeFalse();
    }

    [Fact]
    public void EncryptAndDecryptSecret_RoundTripsWithAesGcm_AndFailsClosedWithoutKey()
    {
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var service = CreateService(Convert.ToBase64String(keyBytes));
        var secret = service.GenerateSecret();

        var encrypted = service.EncryptSecret(secret);
        encrypted.IsSuccess.Should().BeTrue();
        encrypted.Value.Should().NotContain(secret);

        var decrypted = service.DecryptSecret(encrypted.Value);
        decrypted.IsSuccess.Should().BeTrue();
        decrypted.Value.Should().Be(secret);

        var misconfigured = CreateService(string.Empty);
        misconfigured.EncryptSecret(secret).IsFailure.Should().BeTrue();
        misconfigured.DecryptSecret(encrypted.Value).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BuildOtpAuthUri_AndRecoveryCodes_FollowExpectedFormats()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();

        var uri = service.BuildOtpAuthUri("Sensus", "admin@sensus.app", secret);
        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain($"secret={secret}");
        uri.Should().Contain("issuer=Sensus");

        var codes = service.GenerateRecoveryCodes();
        codes.Should().HaveCount(8);
        codes.Distinct().Should().HaveCount(8);

        var hashes = codes.Select(service.HashRecoveryCode).ToList();
        hashes.Should().OnlyContain(h => h.Length == 64);

        service.VerifyRecoveryCode(codes[0], hashes, out var matched).Should().BeTrue();
        matched.Should().Be(hashes[0]);
        service.VerifyRecoveryCode("INVALID-CODE", hashes, out _).Should().BeFalse();
    }
}
