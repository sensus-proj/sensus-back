using FluentAssertions;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;
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

    [Fact]
    public void CreateAndUpdateDetails_ShouldPersistAndPreserveFotoFachadaUrl()
    {
        Comercio comercio = Comercio.Create(
            Guid.NewGuid(),
            "Panadería Centro",
            "Av. Reforma 123",
            "06600",
            19.4326,
            -99.1332,
            fotoFachadaUrl: " https://cdn.sensus.com/fachada-1.jpg ").Value;

        comercio.FotoFachadaUrl.Should().Be("https://cdn.sensus.com/fachada-1.jpg");

        Result preserveResult = comercio.UpdateDetails(
            "Panadería Centro",
            "Av. Reforma 123",
            "06600",
            19.4326,
            -99.1332);
        preserveResult.IsSuccess.Should().BeTrue();
        comercio.FotoFachadaUrl.Should().Be("https://cdn.sensus.com/fachada-1.jpg");

        Result updatePhotoResult = comercio.UpdateDetails(
            "Panadería Centro",
            "Av. Reforma 123",
            "06600",
            19.4326,
            -99.1332,
            fotoFachadaUrl: "https://cdn.sensus.com/fachada-2.jpg");
        updatePhotoResult.IsSuccess.Should().BeTrue();
        comercio.FotoFachadaUrl.Should().Be("https://cdn.sensus.com/fachada-2.jpg");
    }

    [Fact]
    public void Create_WhenRequiredFieldsMissingOrInvalid_ReturnsValidationErrors()
    {
        // Usuario vacío
        Comercio.Create(Guid.Empty, "Comercio", "Dir", "12345", 19.4, -99.1)
            .Error.Code.Should().Be("Comercio.UsuarioInvalido");

        // Nombre vacío
        Comercio.Create(Guid.NewGuid(), "  ", "Dir", "12345", 19.4, -99.1)
            .Error.Code.Should().Be("Comercio.NombreComercialVacio");

        // Dirección vacía
        Comercio.Create(Guid.NewGuid(), "Comercio", "  ", "12345", 19.4, -99.1)
            .Error.Code.Should().Be("Comercio.DireccionVacia");

        // Código postal vacío
        Comercio.Create(Guid.NewGuid(), "Comercio", "Dir", "  ", 19.4, -99.1)
            .Error.Code.Should().Be("Comercio.CodigoPostalVacio");

        // Categoría inválida
        Comercio.Create(Guid.NewGuid(), "Comercio", "Dir", "12345", 19.4, -99.1, categoria: (Sensus.Domain.Enums.MerchantCategory)999)
            .Error.Code.Should().Be("Comercio.CategoriaInvalida");
    }

    [Fact]
    public void UpdateDetails_WhenFieldsMissingOrInvalid_ReturnsValidationErrors()
    {
        var comercio = Comercio.Create(Guid.NewGuid(), "Comercio", "Dir", "12345", 19.4, -99.1).Value;

        comercio.UpdateDetails("  ", "Dir", "12345", 19.4, -99.1).Error.Code.Should().Be("Comercio.NombreComercialVacio");
        comercio.UpdateDetails("Comercio", "  ", "12345", 19.4, -99.1).Error.Code.Should().Be("Comercio.DireccionVacia");
        comercio.UpdateDetails("Comercio", "Dir", "  ", 19.4, -99.1).Error.Code.Should().Be("Comercio.CodigoPostalVacio");
        comercio.UpdateDetails("Comercio", "Dir", "12345", 19.4, -99.1, categoria: (Sensus.Domain.Enums.MerchantCategory)999).Error.Code.Should().Be("Comercio.CategoriaInvalida");
    }

    [Fact]
    public void HasPhysicalLocationChanged_DetectsChangesAccurately()
    {
        var comercio = Comercio.Create(Guid.NewGuid(), "Comercio", "Av. Reforma 123", "06600", 19.4, -99.1).Value;

        comercio.HasPhysicalLocationChanged("Av. Reforma 123", "06600").Should().BeFalse();
        comercio.HasPhysicalLocationChanged("av. reforma 123", "06600").Should().BeFalse();
        comercio.HasPhysicalLocationChanged("Calle Nueva 456", "06600").Should().BeTrue();
        comercio.HasPhysicalLocationChanged("Av. Reforma 123", "06601").Should().BeTrue();
    }

    [Fact]
    public void Reconstitute_AndLifecycleMethods_WorkCorrectly()
    {
        var horario = HorarioDia.Abierto(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)).Value;
        var comercio = Comercio.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tienda",
            "Calle 1",
            "11000",
            19.4,
            -99.1,
            false,
            DateTime.UtcNow.AddDays(-10),
            MerchantCategory.Cafeterias,
            [horario],
            "https://foto.jpg");

        comercio.Activo.Should().BeFalse();
        comercio.Horarios.Should().HaveCount(1);
        comercio.FotoFachadaUrl.Should().Be("https://foto.jpg");

        comercio.Activate();
        comercio.Activo.Should().BeTrue();

        comercio.Deactivate();
        comercio.Activo.Should().BeFalse();

        var nuevoHorario = HorarioDia.Abierto(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(20, 0)).Value;
        comercio.UpdateHorarios([nuevoHorario]);
        comercio.Horarios.Should().ContainSingle().Which.Dia.Should().Be(DayOfWeek.Tuesday);
    }
}
