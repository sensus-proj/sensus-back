using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Social;

/// <summary>
/// Pruebas de integración HTTP para reseñas de comercio (Spec 056), likes de ofertas (Spec 057)
/// y actividad de amigos en comunidad (Spec 058), más moderación determinística anti-groserías (Spec 059).
/// </summary>
public sealed class SocialEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SocialEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.StoreReviewRepository.ClearReceivedCalls();
        _factory.OfferLikeRepository.ClearReceivedCalls();
        _factory.FriendRequestRepository.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Ana Social", $"ana.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return (token, user);
    }

    private string CreateGuestToken()
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        return tokenGenerator.GenerateGuestToken(Guid.NewGuid()).Token;
    }

    private static Comercio BuildMerchant(Guid merchantId, Guid ownerId) =>
        Comercio.Reconstitute(
            merchantId,
            ownerId,
            "Café Sensus",
            "Av. Hidalgo 42",
            "06000",
            19.4326,
            -99.1332,
            activo: true,
            DateTime.UtcNow,
            MerchantCategory.Cafeterias);

    private static Offer BuildOffer(Guid merchantId) =>
        Offer.Create(
            merchantId,
            "Pan dulce artesanal",
            "Caja con 4 piezas recién horneadas",
            50m,
            20,
            10,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            19.4326,
            -99.1332,
            DateTime.UtcNow).Value;

    [Fact]
    public async Task ToggleOfferLike_ReturnsUnauthorized_WithoutToken()
    {
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/offers/{Guid.NewGuid()}/like", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToggleOfferLike_ReturnsForbidden_ForGuest()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/offers/{Guid.NewGuid()}/like", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ToggleOfferLike_ReturnsOkWithUpdatedCounts_ForRegisteredUser()
    {
        (string token, User user) = CreateRegisteredToken();
        Offer offer = BuildOffer(Guid.NewGuid());
        Guid offerId = offer.Id;
        _factory.OfferRepository.GetByIdAsync(offerId, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferLikeRepository
            .ToggleLikeAsync(user.Id, offerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.OfferLikeRepository
            .CountByOfferIdAsync(offerId, Arg.Any<CancellationToken>())
            .Returns(3);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/offers/{offerId}/like", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OfferLikeStatusDto? dto = await response.Content.ReadFromJsonAsync<OfferLikeStatusDto>();
        dto.Should().NotBeNull();
        dto!.OfferId.Should().Be(offerId);
        dto.IsLikedByMe.Should().BeTrue();
        dto.LikesCount.Should().Be(3);
    }

    [Fact]
    public async Task StoreReviewsEndpoints_RejectProfanityAndReturnSummaryOnValidReview()
    {
        (string token, User user) = CreateRegisteredToken();
        Guid merchantId = Guid.NewGuid();
        Comercio merchant = BuildMerchant(merchantId, Guid.NewGuid());
        _factory.ComercioRepository.GetByIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns(merchant);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage badResponse = await _client.PostAsJsonAsync(
            $"/api/v1/merchants/{merchantId}/reviews",
            new { rating = 5, comment = "Esto es una puta estafa" });
        badResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createdReview = StoreReview.Create(
            merchantId,
            user.Id,
            user.NombreCompleto,
            user.FotoPerfilUrl,
            5,
            "Muy buen café y servicio",
            DateTime.UtcNow).Value;
        _factory.StoreReviewRepository
            .GetByMerchantIdAsync(merchantId, Arg.Any<CancellationToken>())
            .Returns([createdReview]);

        HttpResponseMessage okResponse = await _client.PostAsJsonAsync(
            $"/api/v1/merchants/{merchantId}/reviews",
            new { rating = 5, comment = "Muy buen café y servicio" });

        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        StoreReviewsSummaryDto? summary = await okResponse.Content.ReadFromJsonAsync<StoreReviewsSummaryDto>();
        summary.Should().NotBeNull();
        summary!.MerchantId.Should().Be(merchantId);
        summary.AverageRating.Should().Be(5.0);
        summary.TotalReviews.Should().Be(1);
    }

    [Fact]
    public async Task ToggleReviewLikeAndReply_RejectGuestAndHandleValidRegisteredRequests()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());
        HttpResponseMessage guestResp = await _client.PostAsync($"/api/v1/merchants/reviews/{Guid.NewGuid()}/like", null);
        guestResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (string token, User user) = CreateRegisteredToken();
        Guid merchantId = Guid.NewGuid();
        var review = StoreReview.Create(merchantId, Guid.NewGuid(), "Carlos", null, 4, "Rico", DateTime.UtcNow).Value;
        _factory.StoreReviewRepository.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage likeResp = await _client.PostAsync($"/api/v1/merchants/reviews/{review.Id}/like", null);
        likeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        ReviewLikeStatusDto? likeStatus = await likeResp.Content.ReadFromJsonAsync<ReviewLikeStatusDto>();
        likeStatus!.IsLikedByMe.Should().BeTrue();
        likeStatus.LikesCount.Should().Be(1);

        HttpResponseMessage replyResp = await _client.PostAsJsonAsync(
            $"/api/v1/merchants/reviews/{review.Id}/replies",
            new { comment = "Gracias por la recomendación" });
        replyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        StoreReviewDto? updated = await replyResp.Content.ReadFromJsonAsync<StoreReviewDto>();
        updated!.Replies.Should().ContainSingle(r => r.AuthorUserId == user.Id);
    }

    [Fact]
    public async Task GetFriendsCommunityActivity_ReturnsAcceptedFriendsActivityOnly()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());
        HttpResponseMessage guestResp = await _client.GetAsync("/api/v1/community/friends-activity");
        guestResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (string token, User me) = CreateRegisteredToken();
        User friend = User.Create("Beto Amigo", $"beto.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(friend.Id, Arg.Any<CancellationToken>()).Returns(friend);

        var friendship = FriendRequest.Create(
            me.Id,
            me.NombreCompleto,
            "SNS-ME1111",
            friend.Id,
            friend.NombreCompleto,
            "SNS-BE2222",
            "QR",
            DateTime.UtcNow.AddDays(-1)).Value;
        friendship.Accept(DateTime.UtcNow);
        _factory.FriendRequestRepository.GetForUserAsync(me.Id, Arg.Any<CancellationToken>()).Returns([friendship]);

        Guid merchantId = Guid.NewGuid();
        Offer offer = BuildOffer(merchantId);
        Guid offerId = offer.Id;
        Comercio merchant = BuildMerchant(merchantId, Guid.NewGuid());
        _factory.OfferRepository.GetByIdAsync(offerId, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.ComercioRepository.GetByIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns(merchant);

        var friendLike = OfferLike.Create(friend.Id, offerId, DateTime.UtcNow.AddMinutes(-5)).Value;
        var friendReview = StoreReview.Create(merchantId, friend.Id, friend.NombreCompleto, null, 5, "Excelente lugar", DateTime.UtcNow.AddMinutes(-2)).Value;

        _factory.OfferLikeRepository
            .GetByUserIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([friendLike]);
        _factory.StoreReviewRepository
            .GetRecentActivityByUserIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([friendReview]);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage okResp = await _client.GetAsync("/api/v1/community/friends-activity?limit=20");

        okResp.StatusCode.Should().Be(HttpStatusCode.OK);
        List<CommunityActivityItemDto>? items = await okResp.Content.ReadFromJsonAsync<List<CommunityActivityItemDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCount(2);
        items.Select(i => i.ActivityType).Should().BeEquivalentTo(["StoreReview", "OfferLike"]);
    }
}
