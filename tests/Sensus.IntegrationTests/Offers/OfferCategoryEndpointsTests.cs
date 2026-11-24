using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

/// <summary>
/// Pruebas de integración HTTP para OfferCategory en creación (Spec 034 FR-034-08) y edición (Spec 035 FR-035-08).
/// </summary>
public sealed class OfferCategoryEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OfferCategoryEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();
    }

    private static (string Token, User User, Comercio Comercio) SetupMerchant(CustomWebApplicationFactory factory)
    {
        User user = User.Create("Comerciante", $"merchant.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        user.AddRole(RoleType.Comerciante);
        Comercio comercio = Comercio.Create(user.Id, "Panadería Centro", "Av. Juárez 1", "06000", 19.43, -99.13).Value;

        factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        factory.ComercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        IJwtTokenGenerator tokenGenerator = factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateToken(user);
        return (token, user, comercio);
    }

    private WebApplicationFactory<Program> CreateStrictFactory() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OfferCategory:RequireStrictValidation"] = "true"
                })));

    [Fact]
    public async Task PostOffer_WhenStrictFlagDisabledByDefault_AllowsLegacyWithoutCategory()
    {
        (string token, _, _) = SetupMerchant(_factory);
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        DateTime now = _factory.FakeClock.UtcNow;
        var body = new
        {
            title = "Pan dulce legado",
            description = "Paquete de piezas recién horneadas",
            originalPrice = 60m,
            discountPercentage = 25,
            stock = 5,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            category = "Panadería"
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/offers", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostOffer_WhenStrictFlagEnabled_RejectsMissingAndInvalidWith400_AndAcceptsValidCategory()
    {
        using WebApplicationFactory<Program> strictFactory = CreateStrictFactory();
        (string token, _, _) = SetupMerchant(_factory);
        using HttpClient client = strictFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        DateTime now = _factory.FakeClock.UtcNow;

        HttpResponseMessage missingResponse = await client.PostAsJsonAsync("/api/v1/offers", new
        {
            title = "Bolillo artesanal",
            description = "Bolillo recién horneado del día",
            originalPrice = 30m,
            discountPercentage = 20,
            stock = 10,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1)
        });

        missingResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string missingJson = await missingResponse.Content.ReadAsStringAsync();
        missingJson.ToLowerInvariant().Should().Contain("category");

        HttpResponseMessage invalidResponse = await client.PostAsJsonAsync("/api/v1/offers", new
        {
            title = "Bolillo artesanal",
            description = "Bolillo recién horneado del día",
            originalPrice = 30m,
            discountPercentage = 20,
            stock = 10,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            category = "Moda"
        });

        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string invalidJson = await invalidResponse.Content.ReadAsStringAsync();
        invalidJson.ToLowerInvariant().Should().Contain("category");

        HttpResponseMessage validResponse = await client.PostAsJsonAsync("/api/v1/offers", new
        {
            title = "Bolillo artesanal",
            description = "Bolillo recién horneado del día",
            originalPrice = 30m,
            discountPercentage = 20,
            stock = 10,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            category = "Bakery"
        });

        validResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        OfferDto? created = await validResponse.Content.ReadFromJsonAsync<OfferDto>();
        created.Should().NotBeNull();
        created!.Category.Should().Be("Bakery");
    }

    [Fact]
    public async Task PostOffer_WithoutTokenOrConsumerRole_ShouldReturn401And403()
    {
        using HttpClient anonClient = _factory.CreateClient();
        DateTime now = _factory.FakeClock.UtcNow;
        var payload = new
        {
            title = "Bolillo artesanal",
            description = "Bolillo recién horneado del día",
            originalPrice = 30m,
            discountPercentage = 20,
            stock = 10,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            category = "Bakery"
        };

        HttpResponseMessage unauth = await anonClient.PostAsJsonAsync("/api/v1/offers", payload);
        unauth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        User consumer = User.Create("Consumidor", $"consumer.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(consumer.Id, Arg.Any<CancellationToken>()).Returns(consumer);
        (string consumerToken, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(consumer);

        using HttpClient consumerClient = _factory.CreateClient();
        consumerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", consumerToken);
        HttpResponseMessage forbidden = await consumerClient.PostAsJsonAsync("/api/v1/offers", payload);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutOffer_WhenStrictFlagEnabled_RejectsEmptyOrInvalidCategoryAndPreservesExisting()
    {
        using WebApplicationFactory<Program> strictFactory = CreateStrictFactory();
        (string token, _, Comercio comercio) = SetupMerchant(_factory);
        DateTime now = _factory.FakeClock.UtcNow;

        Offer existing = Offer.Create(
            comercio.Id,
            "Concha de vainilla",
            "Pan dulce recién horneado",
            40m,
            25,
            8,
            now.AddMinutes(-10),
            now.AddDays(1),
            nowUtc: now,
            category: "Bakery",
            requireValidCategory: true).Value;

        _factory.OfferRepository.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _factory.OfferClaimRepository.GetActiveClaimCountAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(0);
        _factory.OfferRepository.UpdateWithVersionCheckAsync(Arg.Any<Offer>(), 1, Arg.Any<CancellationToken>()).Returns(true);

        using HttpClient client = strictFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage emptyResp = await client.PutAsJsonAsync($"/api/v1/offers/{existing.Id}", new
        {
            title = "Concha de vainilla",
            description = "Pan dulce recién horneado",
            originalPrice = 40m,
            discountPercentage = 25,
            stock = 8,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            expectedVersion = 1,
            category = ""
        });
        emptyResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        existing.Category.Should().Be("Bakery");

        HttpResponseMessage invalidResp = await client.PutAsJsonAsync($"/api/v1/offers/{existing.Id}", new
        {
            title = "Concha de vainilla",
            description = "Pan dulce recién horneado",
            originalPrice = 40m,
            discountPercentage = 25,
            stock = 8,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            expectedVersion = 1,
            category = "Moda"
        });
        invalidResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        existing.Category.Should().Be("Bakery");

        HttpResponseMessage validResp = await client.PutAsJsonAsync($"/api/v1/offers/{existing.Id}", new
        {
            title = "Concha de vainilla",
            description = "Pan dulce recién horneado",
            originalPrice = 40m,
            discountPercentage = 25,
            stock = 8,
            startsAt = now.AddMinutes(5),
            endsAt = now.AddDays(1),
            expectedVersion = 1,
            category = "Dairy"
        });
        validResp.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await validResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("category").GetString().Should().Be("Dairy");
    }
}
