using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Comercios.Commands.RegisterComercio;
using Sensus.Application.Comercios.Commands.UpdateMerchantProfile;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Merchants;

public class MerchantProfileEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MerchantProfileEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string CreateTokenForUser(User user)
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldSucceed_WhenUserIsMerchant()
    {
        // Arrange
        var merchantUser = User.Create(
            "Dueño Comercio",
            "dueno@comercio.com",
            "hashed-pw",
            true,
            DateTime.UtcNow).Value;

        merchantUser.AddRole(RoleType.Comerciante);

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(merchantUser);

        var token = CreateTokenForUser(merchantUser);

        var existingComercio = Comercio.Create(
            merchantUser.Id,
            "Mi Tiendita",
            "Calle Antigua 123",
            "01000",
            19.4000,
            -99.1500).Value;

        _factory.ComercioRepository.GetByUsuarioIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingComercio);

        _factory.FakeGeocoding.SetupCoordinates(19.4326, -99.1332);

        var command = new UpdateMerchantProfileCommand(
            "Mi Tiendita Renovada",
            "Av. Reforma 222",
            "06600",
            [
                new HorarioDiaDto(DayOfWeek.Monday, "08:00", "20:00", false),
                new HorarioDiaDto(DayOfWeek.Sunday, null, null, true)
            ]);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ComercioDto>();
        dto.Should().NotBeNull();
        dto!.NombreComercial.Should().Be("Mi Tiendita Renovada");
        dto.Direccion.Should().Be("Av. Reforma 222");
        dto.CodigoPostal.Should().Be("06600");
        dto.Latitud.Should().Be(19.4326);
        dto.Longitud.Should().Be(-99.1332);
        dto.Horarios.Should().HaveCount(2);

        await _factory.ComercioRepository.Received(1).UpdateAsync(existingComercio, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnForbidden_WhenUserIsConsumerOnly()
    {
        // Consumidor sin rol comerciante
        var consumerUser = User.Create(
            "Consumidor Normal",
            "consumidor@sensus.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(consumerUser.Id, Arg.Any<CancellationToken>())
            .Returns(consumerUser);

        var token = CreateTokenForUser(consumerUser);

        var command = new UpdateMerchantProfileCommand(
            "No Autorizado",
            "Av. Sin Permiso 123",
            "03100",
            null);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnForbidden_WhenUserIsGuest()
    {
        var guestUser = User.Create(
            "Invitado",
            "guest@sensus.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        typeof(User).GetField("_roles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(guestUser, new List<string> { RoleType.Invitado });

        var token = CreateTokenForUser(guestUser);

        var command = new UpdateMerchantProfileCommand(
            "Intento Invitado",
            "Calle 123",
            "01000",
            null);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnNotFound_WhenMerchantHasNoComercio()
    {
        var merchantUser = User.Create(
            "Comerciante Sin Local",
            "sinlocal@comercio.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        merchantUser.AddRole(RoleType.Comerciante);

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(merchantUser);

        _factory.ComercioRepository.GetByUsuarioIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns((Comercio?)null);

        var token = CreateTokenForUser(merchantUser);

        var command = new UpdateMerchantProfileCommand(
            "Mi Tienda Inexistente",
            "Calle 123",
            "01000",
            null);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnUnprocessable_WhenAddressCannotBeGeocoded()
    {
        var merchantUser = User.Create(
            "Comerciante",
            "failgeo@comercio.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        merchantUser.AddRole(RoleType.Comerciante);

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(merchantUser);

        var existingComercio = Comercio.Create(
            merchantUser.Id,
            "Tienda",
            "Calle Antigua 1",
            "01000",
            19.0,
            -99.0).Value;

        _factory.ComercioRepository.GetByUsuarioIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(existingComercio);

        _factory.FakeGeocoding.SetupFailure();

        var token = CreateTokenForUser(merchantUser);

        var command = new UpdateMerchantProfileCommand(
            "Tienda",
            "Calle Inexistente En el Mapa 9999",
            "99999",
            null);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnBadRequest_WhenZipCodeIsInvalid()
    {
        var merchantUser = User.Create(
            "Comerciante",
            "badcp@comercio.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        merchantUser.AddRole(RoleType.Comerciante);

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>())
            .Returns(merchantUser);

        var token = CreateTokenForUser(merchantUser);

        var command = new UpdateMerchantProfileCommand(
            "Tienda",
            "Calle Correcta 123",
            "CP-INVALIDO",
            null);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ActivateMerchant_ShouldReturnCreated_WithClientCoordinates()
    {
        var user = User.Create(
            "Usuario Nuevo",
            "activar@sensus.com",
            "pw",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _factory.ComercioRepository.ExistsByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var token = CreateTokenForUser(user);
        var command = new RegisterComercioCommand(
            "Panadería La Esperanza",
            "Av. Hidalgo 45, Centro",
            "42800",
            null,
            20.056789,
            -99.341234);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchants/activate")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterComercioResponseDto>();
        body.Should().NotBeNull();
        body!.EsComerciante.Should().BeTrue();
        body.Comercio.Latitud.Should().Be(20.056789);
        body.Comercio.Longitud.Should().Be(-99.341234);
    }

    [Fact]
    public async Task ActivateMerchant_ShouldReturnBadRequest_WhenCoordinatesOutOfRange()
    {
        var user = User.Create("Usuario Bad Coords", "badcoords@sensus.com", "pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var token = CreateTokenForUser(user);

        var command = new RegisterComercioCommand("Panadería", "Av. Hidalgo 45", "42800", null, 120.0, -99.3);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchants/activate")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMyMerchant_ShouldReturnOk_WhenUserIsMerchant()
    {
        var merchantUser = User.Create("Comerciante", "me@comercio.com", "pw", true, DateTime.UtcNow).Value;
        merchantUser.AddRole(RoleType.Comerciante);

        var comercio = Comercio.Create(
            merchantUser.Id,
            "Mi Tienda Oficial",
            "Av. Reforma 100",
            "06600",
            19.4326,
            -99.1332).Value;

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns(merchantUser);
        _factory.ComercioRepository.GetByUsuarioIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        var token = CreateTokenForUser(merchantUser);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/merchants/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ComercioDto>();
        dto.Should().NotBeNull();
        dto!.NombreComercial.Should().Be("Mi Tienda Oficial");
    }

    [Fact]
    public async Task GetNearbyMerchants_ShouldReturnOk_WithNearbyComercios()
    {
        var user = User.Create("Vecino", "vecino.nearby@sensus.com", "pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var token = CreateTokenForUser(user);

        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Tienda Vecina",
            "Av. Hidalgo 10",
            "42800",
            20.0536,
            -99.3436).Value;

        _factory.ComercioRepository
            .GetNearbyAsync(20.0536, -99.3436, 2000, 2, 1, Arg.Any<CancellationToken>())
            .Returns((new List<Comercio> { comercio }, 3));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/merchants/nearby?lat=20.0536&lng=-99.3436&radiusKm=2&radioMetros=1000&page=2&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ComercioDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle(c => c.NombreComercial == "Tienda Vecina");
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
    }

    [Theory]
    [InlineData("lat=91&lng=0")]
    [InlineData("lat=0&lng=-181")]
    [InlineData("lat=NaN&lng=0")]
    [InlineData("lat=0&lng=0&radiusKm=0")]
    [InlineData("lat=0&lng=0&page=0")]
    [InlineData("lat=0&lng=0&pageSize=101")]
    public async Task GetNearbyMerchants_WithInvalidQuery_ReturnsBadRequest(string query)
    {
        var user = User.Create("Vecino", "vecino.validation@sensus.com", "pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/merchants/nearby?{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task GetNearbyMerchants_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var response = await _client.GetAsync("/api/v1/merchants/nearby?lat=0&lng=0");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNearbyMerchants_ExceedingReadLimit_ReturnsTooManyRequests()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:ReadHeavy:PermitLimit", "1"));
        using var client = factory.CreateClient();
        var user = User.Create("Vecino", "vecino.ratelimit@sensus.com", "pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _factory.ComercioRepository.GetNearbyAsync(0, 0, 2000, 1, 20, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Comercio>(), 0));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        using var first = await client.GetAsync("/api/v1/merchants/nearby?lat=0&lng=0");
        using var limited = await client.GetAsync("/api/v1/merchants/nearby?lat=0&lng=0");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ActivateMerchant_ShouldReturnConflict_WhenUserAlreadyHasComercio()
    {
        var user = User.Create("Comerciante Dup", "dup@comercio.com", "pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _factory.ComercioRepository.ExistsByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(true);

        var token = CreateTokenForUser(user);
        var command = new RegisterComercioCommand("Panadería", "Av. Hidalgo 45", "42800", null, 20.05, -99.34);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchants/activate")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetMyMerchant_ShouldReturnNotFound_WhenMerchantDoesNotExist()
    {
        var merchantUser = User.Create("Sin Comercio", "nocomer@comercio.com", "pw", true, DateTime.UtcNow).Value;
        merchantUser.AddRole(RoleType.Comerciante);

        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns(merchantUser);
        _factory.ComercioRepository.GetByUsuarioIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns((Comercio?)null);

        var token = CreateTokenForUser(merchantUser);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/merchants/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnBadRequest_WhenCategoryIsInvalid()
    {
        var merchantUser = User.Create("Comerciante Cat", "badcat@comercio.com", "pw", true, DateTime.UtcNow).Value;
        merchantUser.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns(merchantUser);

        var token = CreateTokenForUser(merchantUser);
        var command = new UpdateMerchantProfileCommand("Tienda", "Calle Correcta 123", "06600", null, "CategoriaInexistente");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldReturnBadRequest_WhenCoordinatesOutOfRange()
    {
        var merchantUser = User.Create("Comerciante Coords", "badcoords.edit@comercio.com", "pw", true, DateTime.UtcNow).Value;
        merchantUser.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(merchantUser.Id, Arg.Any<CancellationToken>()).Returns(merchantUser);

        var token = CreateTokenForUser(merchantUser);
        var command = new UpdateMerchantProfileCommand("Tienda", "Calle Correcta 123", "06600", null, null, 95.0, -99.13);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/merchants/profile")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
