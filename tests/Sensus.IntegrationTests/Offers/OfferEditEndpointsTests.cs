using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Offers;

/// <summary>
/// Pruebas de integración para PUT /api/v1/offers/{id} (Spec 035 / T035-09).
/// Valida: edición 200 con versión incrementada, 401, 403, 404, 409, 400 por stock menor a apartados activos,
/// re-moderación fail-closed y fallo de auditoría fail-closed.
/// </summary>
public sealed class OfferEditEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OfferEditEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
    }

    [Fact]
    public async Task EditOffer_Successful_Returns200WithIncrementedVersion()
    {
        var (owner, comercio, offer, token) = SetupOfferWithMerchant(version: 1);

        _factory.OfferRepository.UpdateWithVersionCheckAsync(Arg.Any<Offer>(), 1, Arg.Any<CancellationToken>())
            .Returns(true);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Café Orgánico Modificado",
            description = "Nueva descripción premium de café de altura",
            originalPrice = 150m,
            discountPercentage = 20,
            stock = 15,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(3),
            imageUrl = "https://res.cloudinary.com/sensus/image/upload/v1/cafe.jpg",
            expectedVersion = 1,
            category = "Food"
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OfferDto>();
        dto.Should().NotBeNull();
        dto!.Version.Should().Be(2);
        dto.Title.Should().Be("Café Orgánico Modificado");
    }

    [Fact]
    public async Task EditOffer_WithoutToken_Returns401Unauthorized()
    {
        var offerId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offerId}");
        request.Content = JsonContent.Create(new
        {
            title = "Cualquier Titulo",
            description = "Descripcion de prueba",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 5,
            startsAt = DateTime.UtcNow,
            endsAt = DateTime.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditOffer_DifferentMerchant_Returns403Forbidden()
    {
        var (_, _, offer, _) = SetupOfferWithMerchant(version: 1);

        // Crear otro comerciante B
        User otherOwner = User.Create("Otro Dueño", $"other.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        otherOwner.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(otherOwner.Id, Arg.Any<CancellationToken>()).Returns(otherOwner);

        Comercio otherComercio = Comercio.Reconstitute(
            Guid.NewGuid(),
            otherOwner.Id,
            "Tienda B",
            "Calle B 20",
            "06000",
            19.4326,
            -99.1332,
            activo: true,
            DateTime.UtcNow,
            MerchantCategory.Cafeterias);
        _factory.ComercioRepository.GetByUsuarioIdAsync(otherOwner.Id, Arg.Any<CancellationToken>()).Returns(otherComercio);

        string otherToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(otherOwner).Token;

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        request.Content = JsonContent.Create(new
        {
            title = "Intento de edición ajena",
            description = "No debería permitirse",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 5,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditOffer_NonExistentOffer_Returns404NotFound()
    {
        var (owner, _, _, token) = SetupOfferWithMerchant(version: 1);
        var nonExistentId = Guid.NewGuid();
        _factory.OfferRepository.GetByIdAsync(nonExistentId, Arg.Any<CancellationToken>())
            .Returns((Offer?)null);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{nonExistentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Oferta Fantasma",
            description = "No existe en la base de datos",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 5,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditOffer_ObsoleteVersion_Returns409Conflict()
    {
        var (_, _, offer, token) = SetupOfferWithMerchant(version: 2);

        // Cliente envía expectedVersion = 1 cuando la oferta ya va en versión 2
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Título Concurrente",
            description = "Descripción concurrente",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 5,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EditOffer_StockBelowActiveClaims_Returns409Conflict()
    {
        var (_, _, offer, token) = SetupOfferWithMerchant(version: 1);

        // Simular que hay 5 apartados activos para esta oferta
        _factory.OfferClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(5);

        // Cliente intenta reducir stock a 3 (menor a los 5 apartados activos)
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Café con stock insuficiente",
            description = "Stock menor que apartados",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 3,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EditOffer_InvalidStock_Returns400BadRequest()
    {
        var (_, _, offer, token) = SetupOfferWithMerchant(version: 1);

        // Cliente intenta enviar stock menor a 1 (infracción de validación)
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Café con stock cero",
            description = "Stock inválido menor a 1",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 0,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditOffer_ModerationRejection_FailClosed_DoesNotPersistChanges()
    {
        var (_, _, offer, token) = SetupOfferWithMerchant(version: 1);
        _factory.OfferRepository.ClearReceivedCalls();

        // Configurar el doble de moderación para rechazar el contenido
        _factory.TextModerationService.ModerateContentTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(
                false,
                Decision: "Rejected",
                FlaggedCategories: ["harassment"]));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Contenido Prohibido",
            description = "Descripción con términos inapropiados",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 10,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Fail-closed: No se persistió la actualización en repositorio
        await _factory.OfferRepository.DidNotReceive()
            .UpdateWithVersionCheckAsync(Arg.Any<Offer>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditOffer_AuditRecorderFailure_FailClosed_DoesNotPersistChanges()
    {
        var (_, _, offer, token) = SetupOfferWithMerchant(version: 1);
        _factory.OfferRepository.ClearReceivedCalls();

        // Moderación aprueba
        _factory.TextModerationService.ModerateContentTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, Decision: "Approved"));

        // Fallo en la auditoría inmutable
        _factory.ModerationAuditRepository.AddAsync(
            Arg.Any<ModerationAuditRecord>(),
            Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Fallo forzado de base de datos en auditoría"));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            title = "Título Nuevo",
            description = "Descripción Nueva",
            originalPrice = 100m,
            discountPercentage = 10,
            stock = 10,
            startsAt = _factory.FakeClock.UtcNow.AddHours(-1),
            endsAt = _factory.FakeClock.UtcNow.AddDays(1),
            expectedVersion = 1
        });

        var response = await _client.SendAsync(request);

        // Debe fallar (500 o error controlado) y NO guardar cambios
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);

        // Fail-closed: No se persistió la actualización en el repositorio
        await _factory.OfferRepository.DidNotReceive()
            .UpdateWithVersionCheckAsync(Arg.Any<Offer>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private (User Owner, Comercio Comercio, Offer Offer, string Token) SetupOfferWithMerchant(int version)
    {
        User owner = User.Create("Comercio Dueño", $"merchant.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        owner.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        Comercio comercio = Comercio.Reconstitute(
            Guid.NewGuid(),
            owner.Id,
            "Tienda Sensus",
            "Av. Insurgentes 100",
            "06000",
            19.4326,
            -99.1332,
            activo: true,
            DateTime.UtcNow,
            MerchantCategory.Cafeterias);
        _factory.ComercioRepository.GetByUsuarioIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        DateTime now = _factory.FakeClock.UtcNow;
        Offer offer = Offer.Create(
            comercio.Id,
            "Café en grano original",
            "Bolsa 500g original",
            120m,
            25,
            10,
            now.AddHours(-1),
            now.AddDays(2),
            null,
            19.4326,
            -99.1332,
            now).Value;

        // Si se requiere versión mayor a 1, simular versión previa
        if (version > 1)
        {
            for (int v = 1; v < version; v++)
            {
                offer.Update(
                    offer.Title,
                    offer.Description,
                    offer.OriginalPrice,
                    offer.DiscountPercentage,
                    offer.Stock,
                    offer.StartsAt,
                    offer.EndsAt,
                    offer.ImageUrl,
                    0,
                    offer.Version,
                    now,
                    category: null,
                    requireValidCategory: false);
            }
        }

        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(0);

        string token = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(owner).Token;
        return (owner, comercio, offer, token);
    }
}
