using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Sensus.Infrastructure.Authentication;

namespace Sensus.IntegrationTests.Authentication;

public class JwtSettingsValidatorTests
{
    private readonly JwtSettingsValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenSecretIsTooShort()
    {
        var settings = new JwtSettings
        {
            Secret = new string('x', 12),
            Issuer = "SensusApi",
            Audience = "SensusApp",
            ExpiryMinutes = 60
        };

        var result = _validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("al menos 32 caracteres");
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenSettingsAreValidAndStrong()
    {
        var settings = new JwtSettings
        {
            Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            Issuer = "SensusApi",
            Audience = "SensusApp",
            ExpiryMinutes = 1440
        };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void Validate_ShouldFail_WhenGuestExpiryMinutesOutOfAllowedRange(int guestMinutes)
    {
        var settings = new JwtSettings
        {
            Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            Issuer = "SensusApi",
            Audience = "SensusApp",
            ExpiryMinutes = 1440,
            GuestExpiryMinutes = guestMinutes
        };

        var result = _validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("GuestExpiryMinutes");
    }
}
