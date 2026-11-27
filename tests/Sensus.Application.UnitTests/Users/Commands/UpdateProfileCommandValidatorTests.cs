using FluentAssertions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;
using Sensus.Application.Users.Commands.UpdateProfile;

namespace Sensus.Application.UnitTests.Users.Commands;

public class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new(
        Options.Create(new CloudinaryAccountSettings { CloudName = "sensus" }));

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var command = new UpdateProfileCommand("Rodrigo Sánchez", "https://res.cloudinary.com/sensus/image/upload/v1234/profile.jpg");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenFotoPerfilUrlIsNull()
    {
        var command = new UpdateProfileCommand("Rodrigo Sánchez", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")] // < 2 chars
    public void Validate_ShouldFail_WhenNombreIsTooShortOrEmpty(string invalidNombre)
    {
        var command = new UpdateProfileCommand(invalidNombre, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.NombreCompleto));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNombreExceeds100Chars()
    {
        var longNombre = new string('A', 101);
        var command = new UpdateProfileCommand(longNombre, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNombreContainsControlChars()
    {
        var command = new UpdateProfileCommand("Rodrigo\u0000Sánchez", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/sensus/profile.jpg")] // Insecure HTTP
    [InlineData("https://malicious-site.com/avatar.jpg")] // Non-cloudinary domain
    [InlineData("https://evilcloudinary.com/sensus/avatar.jpg")] // Suffix spoofed domain
    [InlineData("https://api.cloudinary.com/sensus/avatar.jpg")] // Subdomain other than res.cloudinary.com
    [InlineData("https://res.cloudinary.com/otra-cuenta/image/upload/v1/avatar.jpg")] // Another Cloudinary account
    [InlineData("not-a-url")]
    public void Validate_ShouldFail_WhenFotoPerfilUrlIsNotSecureCloudinary(string invalidUrl)
    {
        var command = new UpdateProfileCommand("Rodrigo Sánchez", invalidUrl);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.FotoPerfilUrl));
    }

    [Fact]
    public void Validate_ShouldFailClosed_WhenCloudNameIsNotConfigured()
    {
        var misconfiguredValidator = new UpdateProfileCommandValidator(
            Options.Create(new CloudinaryAccountSettings { CloudName = string.Empty }));
        var command = new UpdateProfileCommand("Rodrigo Sánchez", "https://res.cloudinary.com/sensus/image/upload/v1/avatar.jpg");

        var result = misconfiguredValidator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
