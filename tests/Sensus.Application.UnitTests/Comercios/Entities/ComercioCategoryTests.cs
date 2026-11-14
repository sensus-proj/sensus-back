using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Comercios.Entities;

public class ComercioCategoryTests
{
    private static readonly DateTime RegistrationDate = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldAssignCategory_WhenCategoryIsValid()
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Panadería Central",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332,
            RegistrationDate,
            MerchantCategory.Cafeterias);

        result.IsSuccess.Should().BeTrue();
        result.Value.Categoria.Should().Be(MerchantCategory.Cafeterias);
    }

    [Fact]
    public void Create_ShouldDefaultToOtros_WhenCategoryIsOmitted()
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Tienda",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332);

        result.IsSuccess.Should().BeTrue();
        result.Value.Categoria.Should().Be(MerchantCategory.Otros);
    }

    [Fact]
    public void Create_ShouldFail_WhenCategoryIsNotDefined()
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Tienda",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332,
            RegistrationDate,
            (MerchantCategory)999);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.CategoriaInvalida");
    }

    [Fact]
    public void UpdateDetails_ShouldUpdateCategory_WhenProvided()
    {
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Tienda",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332).Value;

        var result = comercio.UpdateDetails(
            "Tienda",
            "Calle Reforma 200",
            "06010",
            19.43,
            -99.13,
            MerchantCategory.Tecnologia);

        result.IsSuccess.Should().BeTrue();
        comercio.Categoria.Should().Be(MerchantCategory.Tecnologia);
    }

    [Fact]
    public void UpdateDetails_ShouldKeepCategory_WhenNotProvided()
    {
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Tienda",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332,
            RegistrationDate,
            MerchantCategory.Moda).Value;

        var result = comercio.UpdateDetails("Tienda", "Calle Reforma 200", "06010", 19.43, -99.13);

        result.IsSuccess.Should().BeTrue();
        comercio.Categoria.Should().Be(MerchantCategory.Moda);
    }

    [Fact]
    public void Reconstitute_ShouldRestoreStateWithoutRaisingDomainEvents()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var comercio = Comercio.Reconstitute(
            id,
            userId,
            "Sucursal Norte",
            "Avenida Norte 500",
            "07000",
            19.5,
            -99.1,
            activo: false,
            RegistrationDate,
            MerchantCategory.Farmacias);

        comercio.Id.Should().Be(id);
        comercio.UsuarioId.Should().Be(userId);
        comercio.Activo.Should().BeFalse();
        comercio.Categoria.Should().Be(MerchantCategory.Farmacias);
        comercio.FechaRegistro.Should().Be(RegistrationDate);
        comercio.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MerchantCategoryCatalog_TryParse_ShouldBeCaseInsensitiveAndRejectUnknownValues()
    {
        MerchantCategoryCatalog.TryParse("restaurantes", out var categoria).Should().BeTrue();
        categoria.Should().Be(MerchantCategory.Restaurantes);

        MerchantCategoryCatalog.TryParse("NoExiste", out _).Should().BeFalse();
        MerchantCategoryCatalog.TryParse("999", out _).Should().BeFalse();
        MerchantCategoryCatalog.TryParse(null, out var fallback).Should().BeFalse();
        fallback.Should().Be(MerchantCategory.Otros);
    }
}
