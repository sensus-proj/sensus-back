using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.IntegrationTests.Persistence;

public class ComercioDocumentMappingTests
{
    [Fact]
    public void FromDomain_And_ToDomain_ShouldRoundTripCategory()
    {
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Farmacia del Centro",
            "Calle Centro 100",
            "06000",
            19.4326,
            -99.1332,
            new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc),
            MerchantCategory.Farmacias).Value;

        var document = ComercioDocument.FromDomain(comercio);
        var restored = document.ToDomain();

        document.Categoria.Should().Be(MerchantCategory.Farmacias);
        restored.Id.Should().Be(comercio.Id);
        restored.UsuarioId.Should().Be(comercio.UsuarioId);
        restored.Categoria.Should().Be(MerchantCategory.Farmacias);
        restored.Activo.Should().BeTrue();
    }

    [Fact]
    public void ToDomain_ShouldFallBackToOtros_WhenDocumentHasNoCategory()
    {
        var document = new ComercioDocument
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            NombreComercial = "Comercio Antiguo",
            Direccion = "Calle Antigua 1",
            CodigoPostal = "06000",
            Latitud = 19.4326,
            Longitud = -99.1332,
            Activo = true,
            FechaRegistro = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var restored = document.ToDomain();

        restored.Categoria.Should().Be(MerchantCategory.Otros);
    }
}
