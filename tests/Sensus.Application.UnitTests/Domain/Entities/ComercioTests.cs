using FluentAssertions;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Domain.Entities;

public class ComercioTests
{
    [Fact]
    public void Create_WithHighPrecisionCoordinates_ShouldRoundToSixDecimals()
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Café Sensus",
            "Av. Reforma 123",
            "06600",
            19.43260789,
            -99.13320845);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitud.Should().Be(19.432608);
        result.Value.Longitud.Should().Be(-99.133208);
    }

    [Theory]
    [InlineData(-90.0001, -99.1332, "Comercio.LatitudInvalida")]
    [InlineData(90.0001, -99.1332, "Comercio.LatitudInvalida")]
    [InlineData(double.NaN, -99.1332, "Comercio.LatitudInvalida")]
    [InlineData(double.PositiveInfinity, -99.1332, "Comercio.LatitudInvalida")]
    [InlineData(19.4326, -180.0001, "Comercio.LongitudInvalida")]
    [InlineData(19.4326, 180.0001, "Comercio.LongitudInvalida")]
    [InlineData(19.4326, double.NaN, "Comercio.LongitudInvalida")]
    [InlineData(19.4326, double.NegativeInfinity, "Comercio.LongitudInvalida")]
    public void Create_WithInvalidCoordinates_ShouldFail(double latitud, double longitud, string expectedErrorCode)
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Café Sensus",
            "Av. Reforma 123",
            "06600",
            latitud,
            longitud);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Fact]
    public void UpdateDetails_WithValidHighPrecisionCoordinates_ShouldNormalizeToSixDecimals()
    {
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Café Sensus",
            "Av. Reforma 123",
            "06600",
            19.4326,
            -99.1332).Value;

        var updateResult = comercio.UpdateDetails(
            "Café Sensus Centro",
            "Av. Juárez 45",
            "06010",
            19.43512349,
            -99.14123456);

        updateResult.IsSuccess.Should().BeTrue();
        comercio.Latitud.Should().Be(19.435123);
        comercio.Longitud.Should().Be(-99.141235);
    }

    [Theory]
    [InlineData(-91.0, -99.1332, "Comercio.LatitudInvalida")]
    [InlineData(19.4326, 181.0, "Comercio.LongitudInvalida")]
    public void UpdateDetails_WithOutOfRangeCoordinates_ShouldFail(double latitud, double longitud, string expectedErrorCode)
    {
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Café Sensus",
            "Av. Reforma 123",
            "06600",
            19.4326,
            -99.1332).Value;

        var updateResult = comercio.UpdateDetails(
            "Café Sensus",
            "Av. Reforma 123",
            "06600",
            latitud,
            longitud);

        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be(expectedErrorCode);
    }
}
