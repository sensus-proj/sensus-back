using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

public sealed class OfferLifecycleEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OfferLifecycleEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(new DateTime(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc));
        _factory.OfferStatusHistoryRepository.ClearReceivedCalls();
    }

    [Fact]
    public async Task ChangeStatusAndRepublish_PersistStatusTransitionHistory()
    {
        User owner = User.Create("Comercio Dueño", $"merchant.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        owner.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        Comercio comercio = Comercio.Reconstitute(
            Guid.NewGuid(),
            owner.Id,
            "Tienda Sol",
            "Av. Central 10",
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
            "Café en grano",
            "Bolsa 500g",
            120m,
            25,
            10,
            now.AddHours(-1),
            now.AddDays(2),
            null,
            19.4326,
            -99.1332,
            now).Value;
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        string token = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(owner).Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage pauseResp = await _client.PutAsJsonAsync(
            $"/api/v1/offers/{offer.Id}/status",
            new { status = "Paused" });
        pauseResp.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.OfferStatusHistoryRepository.Received(1).AddAsync(
            Arg.Is<OfferStatusTransition>(t =>
                t.OfferId == offer.Id
                && t.FromStatus == OfferStatus.Published
                && t.ToStatus == OfferStatus.Paused),
            Arg.Any<CancellationToken>());

        HttpResponseMessage archiveResp = await _client.PutAsJsonAsync(
            $"/api/v1/offers/{offer.Id}/status",
            new { status = "Archived" });
        archiveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage republishResp = await _client.PostAsJsonAsync(
            $"/api/v1/offers/{offer.Id}/republish",
            new { newStartsAt = now, newEndsAt = now.AddDays(3), newStock = 20 });
        republishResp.StatusCode.Should().Be(HttpStatusCode.OK);
        OfferDto? republished = await republishResp.Content.ReadFromJsonAsync<OfferDto>();
        republished.Should().NotBeNull();
        republished!.Stock.Should().Be(20);

        await _factory.OfferStatusHistoryRepository.Received(1).AddAsync(
            Arg.Is<OfferStatusTransition>(t =>
                t.OfferId == offer.Id
                && t.FromStatus == OfferStatus.Archived
                && t.ToStatus == OfferStatus.Published
                && t.TransitionType == "Republish"),
            Arg.Any<CancellationToken>());
    }
}
