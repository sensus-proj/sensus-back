using Amazon.Extensions.Configuration.SystemsManager;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Sensus.Api.Extensions;
using Xunit;

namespace Sensus.IntegrationTests.Security;

/// <summary>
/// Principio VIII (regla 5) y brecha G-01: en Lambda los secretos se leen de SSM Parameter Store, como fuente
/// obligatoria y de mayor prioridad; fuera de Lambda no se consulta AWS.
/// </summary>
public sealed class SecretsConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutPrefix_DoesNotTouchAws(string? prefix)
    {
        var builder = new ConfigurationBuilder();

        bool added = builder.AddSensusSecretStore(prefix);

        added.Should().BeFalse();
        builder.Sources.Should().BeEmpty();
    }

    [Fact]
    public void WithPrefix_AddsParameterStoreAsMandatoryAndLastSource()
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:Secret"] = "de-entorno" });

        bool added = builder.AddSensusSecretStore("/sensus/prod");

        added.Should().BeTrue();
        var source = builder.Sources.Last().Should().BeOfType<SystemsManagerConfigurationSource>().Subject;
        source.Path.Should().Be("/sensus/prod");
        source.Optional.Should().BeFalse("si SSM no responde, la API no debe arrancar sin secretos (fail-closed)");
    }

    [Fact]
    public void WithRelativePrefix_FailsFast()
    {
        var act = () => new ConfigurationBuilder().AddSensusSecretStore("sensus/prod");

        act.Should().Throw<InvalidOperationException>();
    }
}
