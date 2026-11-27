using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Services;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClaimOffer_UsesFcmContractAndRespectsMerchantPreferences(bool merchantPushEnabled)
    {
        string buyerToken = CreateConsumerToken(out Guid buyerId);
        var (_, merchant) = CreateMerchantToken();
        var now = DateTime.UtcNow;
        var offer = Offer.Create(merchant.Id, "Oferta para recoger", "Disponible en el local", 100m, 20, 2,
            now.AddHours(-1), now.AddDays(1), nowUtc: now).Value;
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferRepository.TryReserveStockAsync(offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _factory.ComercioRepository.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        OfferClaim? saved = null;
        _factory.OfferClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<OfferClaim>();
            return true;
        });

        var devices = Substitute.For<IDeviceRepository>();
        devices.GetActiveByUserAsync(buyerId, Arg.Any<CancellationToken>())
            .Returns(new List<Device> { Device.Create(buyerId, "buyer", "Android", "buyer-test-device", now) });
        devices.GetActiveByUserAsync(merchant.UsuarioId, Arg.Any<CancellationToken>())
            .Returns(new List<Device> { Device.Create(merchant.UsuarioId, "merchant", "Android", "merchant-test-device", now) });
        var settings = NotificationSettings.CreateDefaults(merchant.UsuarioId);
        settings.Update(merchantPushEnabled, null, null, null);
        var preferences = Substitute.For<INotificationSettingsRepository>();
        preferences.GetByUserIdAsync(merchant.UsuarioId, Arg.Any<CancellationToken>()).Returns(settings);
        var receipts = Substitute.For<IClaimPushReceiptStore>();
        receipts.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        var payloads = new ConcurrentBag<JsonElement>();
        using var http = new HttpClient(new ClaimFcmHandler(async (request, ct) =>
        {
            Assert.NotNull(saved);
            Assert.Equal("/v1/projects/sensus-test/messages:send", request.RequestUri!.AbsolutePath);
            using var body = await request.Content!.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            payloads.Add(body!.RootElement.Clone());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { name = "projects/sensus-test/messages/accepted" })
            };
        }))
        {
            BaseAddress = new Uri("https://fcm.googleapis.com/")
        };
        var dispatcher = new FcmPushDispatcher(http, _ => Task.FromResult(("sensus-test", "test-placeholder")),
            NullLogger<FcmPushDispatcher>.Instance);
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureServices((_, services) =>
        {
            services.Replace(ServiceDescriptor.Singleton(devices));
            services.Replace(ServiceDescriptor.Singleton(preferences));
            services.Replace(ServiceDescriptor.Singleton(receipts));
            services.Replace(ServiceDescriptor.Singleton<IPushDispatcher>(dispatcher));
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var response = await client.PostAsync($"/api/v1/offers/{offer.Id}/claim", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var claim = await response.Content.ReadFromJsonAsync<ClaimOfferResultDto>();
        Assert.NotNull(claim);
        Assert.Equal(claim.Code, claim.QrPayload);
        Assert.Equal(merchantPushEnabled ? 2 : 1, payloads.Count);
        foreach (var payload in payloads)
        {
            Assert.False(payload.GetProperty("validate_only").GetBoolean());
            var message = payload.GetProperty("message");
            var data = message.GetProperty("data");
            bool isBuyer = message.GetProperty("token").GetString() == "buyer-test-device";
            Assert.Equal(isBuyer ? "claim_confirmed" : "merchant_offer_claimed", data.GetProperty("type").GetString());
            Assert.Equal(claim.ClaimId, data.GetProperty("claimId").GetGuid());
            Assert.Equal(offer.Id, data.GetProperty("offerId").GetGuid());
            Assert.Equal(isBuyer ? 4 : 3, data.EnumerateObject().Count());
            if (isBuyer)
            {
                Assert.Equal(claim.ExpiresAtUtc, data.GetProperty("expiresAtUtc").GetDateTime());
            }
        }
        Assert.Contains(payloads, payload => payload.GetProperty("message").GetProperty("token").GetString() == "buyer-test-device");
    }

    private sealed class ClaimFcmHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
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

    [Fact]
    public async Task ClaimOffer_WithQuantityFour_AndCartClaims_ShouldReturnSingleQr()
    {
        Offer first = BuildActiveOffer();
        Offer second = Offer.Create(
            first.MerchantId,
            "Galletas de avena",
            "Paquete de galletas artesanales horneadas",
            50m,
            20,
            10,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            20.05,
            -99.34,
            DateTime.UtcNow).Value;

        _factory.OfferRepository.GetByIdAsync(first.Id, Arg.Any<CancellationToken>()).Returns(first);
        _factory.OfferRepository.GetByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);
        _factory.OfferRepository.TryReserveStockAsync(first.Id, 4, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.OfferRepository.TryReserveStockAsync(second.Id, 2, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.OfferClaimRepository
            .GetActiveByUserAndAnyOfferAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _factory.OfferClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>())
            .Returns(true);

        using var cartRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/offers/claims")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { offerId = first.Id, quantity = 4 },
                    new { offerId = second.Id, quantity = 2 }
                }
            })
        };
        cartRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateConsumerToken());

        HttpResponseMessage response = await _client.SendAsync(cartRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ClaimOfferResultDto? dto = await response.Content.ReadFromJsonAsync<ClaimOfferResultDto>();
        dto.Should().NotBeNull();
        dto!.TotalUnits.Should().Be(6);
        dto.Items.Should().HaveCount(2);
        dto.QrPayload.Should().StartWith("v1.");
    }
}
