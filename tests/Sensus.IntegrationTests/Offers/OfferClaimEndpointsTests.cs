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

public class OfferClaimEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OfferClaimEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();
    }

    private string CreateConsumerToken() => CreateConsumerToken(out _);

    private string CreateConsumerToken(out Guid userId)
    {
        User user = User.Create("Consumidor", $"consumidor.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        userId = user.Id;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    [Fact]
    public async Task GetOfferClaim_ReturnsQrOnlyToBuyer()
    {
        string buyerToken = CreateConsumerToken(out Guid buyerId);
        var (merchantToken, merchant) = CreateMerchantToken();
        OfferClaim claim = OfferClaim.Create(buyerId, Guid.NewGuid(), merchant.Id, DateTime.UtcNow).Value;
        ClaimCode code = _factory.Services.GetRequiredService<IClaimCodeService>()
            .Generate(claim.Id, claim.OfferId, claim.ExpiresAtUtc);
        claim.AssignCodeHash(code.Hash);
        _factory.OfferClaimRepository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _factory.ComercioRepository.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        string url = $"/api/v1/offers/claims/{claim.Id}";

        using HttpResponseMessage anonymous = await _client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (guestToken, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        using HttpResponseMessage guest = await _client.GetAsync(url);
        guest.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());
        using HttpResponseMessage thirdParty = await _client.GetAsync(url);
        thirdParty.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);
        using HttpResponseMessage merchantResponse = await _client.GetAsync(url);
        merchantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await merchantResponse.Content.ReadFromJsonAsync<OfferClaimDetailDto>())!.QrPayload.Should().BeNull();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using HttpResponseMessage buyerResponse = await _client.GetAsync(url);
        buyerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        buyerResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await buyerResponse.Content.ReadFromJsonAsync<OfferClaimDetailDto>())!.QrPayload.Should().Be(code.QrPayload);
    }

    [Fact]
    public async Task GetOfferClaim_ReturnsExpiredWithoutQrWhenExpiryJobHasNotRun()
    {
        string buyerToken = CreateConsumerToken(out Guid buyerId);
        OfferClaim claim = OfferClaim.Create(buyerId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-2)).Value;
        _factory.OfferClaimRepository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using HttpResponseMessage response = await _client.GetAsync($"/api/v1/offers/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadFromJsonAsync<OfferClaimDetailDto>())!;
        detail.Status.Should().Be("Expired");
        detail.QrPayload.Should().BeNull();
    }

    [Fact]
    public async Task GetOfferClaim_OmitsQrWhenPersistedSignatureWasReplaced()
    {
        string buyerToken = CreateConsumerToken(out Guid buyerId);
        OfferClaim claim = OfferClaim.Create(buyerId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow).Value;
        claim.AssignCodeHash("replaced-signature-hash");
        _factory.OfferClaimRepository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using HttpResponseMessage response = await _client.GetAsync($"/api/v1/offers/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var detail = (await response.Content.ReadFromJsonAsync<OfferClaimDetailDto>())!;
        detail.Status.Should().Be("Active");
        detail.QrPayload.Should().BeNull();
    }

    [Fact]
    public async Task GetOfferClaim_ReturnsNotFoundForMissingClaimOrInactiveMerchant()
    {
        var (merchantToken, merchant) = CreateMerchantToken();
        merchant.Deactivate();
        OfferClaim claim = OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), merchant.Id, DateTime.UtcNow).Value;
        _factory.OfferClaimRepository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _factory.ComercioRepository.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);

        using HttpResponseMessage inactiveMerchant = await _client.GetAsync($"/api/v1/offers/claims/{claim.Id}");
        using HttpResponseMessage missing = await _client.GetAsync($"/api/v1/offers/claims/{Guid.NewGuid()}");

        inactiveMerchant.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private (string Token, Comercio Comercio) CreateMerchantToken()
    {
        User user = User.Create("Comerciante", $"comerciante.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        user.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Comercio comercio = Comercio.Create(
            user.Id,
            "Tienda del Barrio",
            "Calle 1",
            "06000",
            19.4326,
            -99.1332,
            DateTime.UtcNow,
            MerchantCategory.Otros).Value;

        _factory.ComercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(comercio);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return (token, comercio);
    }

    private static Offer BuildActiveOffer() =>
        Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            -12.05,
            -77.04,
            DateTime.UtcNow).Value;

    [Fact]
    public async Task ClaimOffer_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/offers/{Guid.NewGuid()}/claim",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaimOffer_ShouldReturnSignedCode_WhenOfferIsAvailable()
    {
        // Arrange
        Offer offer = BuildActiveOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferRepository.TryReserveStockAsync(offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.OfferClaimRepository
            .GetActiveByUserAndOfferAsync(Arg.Any<Guid>(), offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _factory.OfferClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>())
            .Returns(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/claim");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ClaimOfferResultDto? dto = await response.Content.ReadFromJsonAsync<ClaimOfferResultDto>();
        dto.Should().NotBeNull();
        dto!.OfferId.Should().Be(offer.Id);
        dto.Code.Should().NotBeNullOrWhiteSpace();
        dto.Code.Should().StartWith("v1.");
        dto.QrPayload.Should().Be(dto.Code);
        dto.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));

        await _factory.OfferClaimRepository.Received(1).TryAddAsync(
            Arg.Any<OfferClaim>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimOffer_ShouldReturnConflict_WhenAnActiveClaimAlreadyExists()
    {
        // Arrange
        Offer offer = BuildActiveOffer();
        OfferClaim existing = OfferClaim.Create(
            Guid.NewGuid(),
            offer.Id,
            offer.MerchantId,
            DateTime.UtcNow).Value;

        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferClaimRepository
            .GetActiveByUserAndOfferAsync(Arg.Any<Guid>(), offer.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/claim");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.OfferRepository.DidNotReceiveWithAnyArgs()
            .TryReserveStockAsync(default, default, default);
    }

    [Fact]
    public async Task ClaimOffer_ShouldReturnConflict_WhenThereIsNoStockLeft()
    {
        // Arrange
        Offer offer = BuildActiveOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferClaimRepository
            .GetActiveByUserAndOfferAsync(Arg.Any<Guid>(), offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _factory.OfferRepository.TryReserveStockAsync(offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/claim");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RedeemClaim_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/offers/claims/v1.abc.def.123.signature/redeem",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RedeemClaim_ShouldReturnForbidden_WhenUserIsNotMerchant()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/offers/claims/v1.abc.def.123.signature/redeem");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RedeemClaim_ShouldReturnOk_WhenCodeIsValidAndMerchantOwnsIt()
    {
        // Arrange
        (string merchantToken, Comercio comercio) = CreateMerchantToken();
        IClaimCodeService codeService = _factory.Services.GetRequiredService<IClaimCodeService>();

        Guid claimId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();
        DateTime now = _factory.FakeClock.UtcNow;
        ClaimCode code = codeService.Generate(claimId, offerId, now.AddHours(24));

        OfferClaim claim = OfferClaim.Reconstitute(
            claimId,
            Guid.NewGuid(),
            offerId,
            comercio.Id,
            codeService.ComputeHash(code.Code),
            OfferClaimStatus.Active,
            now.AddHours(24),
            null,
            now.AddHours(-1));

        _factory.OfferClaimRepository
            .GetByCodeHashAsync(codeService.ComputeHash(code.Code), Arg.Any<CancellationToken>())
            .Returns(claim);
        _factory.OfferClaimRepository
            .TryRedeemAsync(claimId, comercio.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/offers/claims/{Uri.EscapeDataString(code.Code)}/redeem");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RedeemClaimResultDto? dto = await response.Content.ReadFromJsonAsync<RedeemClaimResultDto>();
        dto.Should().NotBeNull();
        dto!.ClaimId.Should().Be(claimId);
        dto.MerchantId.Should().Be(comercio.Id);
    }

    [Fact]
    public async Task RedeemClaim_ShouldReturnBadRequest_WhenCodeSignatureIsInvalid()
    {
        (string merchantToken, _) = CreateMerchantToken();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/offers/claims/v1.abc.def.123.signature/redeem");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RedeemClaim_ShouldReturnConflict_WhenClaimWasAlreadyRedeemed()
    {
        // Arrange
        (string merchantToken, Comercio comercio) = CreateMerchantToken();
        IClaimCodeService codeService = _factory.Services.GetRequiredService<IClaimCodeService>();

        Guid claimId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();
        DateTime now = _factory.FakeClock.UtcNow;
        ClaimCode code = codeService.Generate(claimId, offerId, now.AddHours(24));

        OfferClaim claim = OfferClaim.Reconstitute(
            claimId,
            Guid.NewGuid(),
            offerId,
            comercio.Id,
            codeService.ComputeHash(code.Code),
            OfferClaimStatus.Active,
            now.AddHours(24),
            null,
            now.AddHours(-1));

        _factory.OfferClaimRepository
            .GetByCodeHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(claim);
        _factory.OfferClaimRepository
            .TryRedeemAsync(claimId, comercio.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/offers/claims/{Uri.EscapeDataString(code.Code)}/redeem");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
