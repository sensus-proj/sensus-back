using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.Commands.CreateOffer;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

public sealed class CreateOfferEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly CreateOfferCommand ValidOffer = new(
        "Oferta de prueba", "Descripción de la oferta", 100m, 25, 3, Now, Now.AddDays(1));

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task CreateOffer_RequiresMerchantAuthorization(bool signedIn, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        if (signedIn)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(false).Token);
        }

        using var response = await client.PostAsJsonAsync("/api/v1/offers", ValidOffer);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(95, false)]
    [InlineData(25, true)]
    public async Task CreateOffer_RejectsInvalidDiscountOrDates(int discount, bool invalidDates)
    {
        var (token, _) = CreateUserToken(true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalid = ValidOffer with { DiscountPercentage = discount, EndsAt = invalidDates ? Now.AddDays(-1) : ValidOffer.EndsAt };

        using var response = await client.PostAsJsonAsync("/api/v1/offers", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOffer_UsesAuthenticatedMerchantAndReturnsCalculatedPrice()
    {
        factory.FakeClock.Set(Now);
        var (token, userId) = CreateUserToken(true);
        var merchant = Comercio.Create(userId, "Tienda Sensus", "Calle Uno 1", "03100", 19.43, -99.13).Value;
        factory.ComercioRepository.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(merchant);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync("/api/v1/offers", ValidOffer);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = await response.Content.ReadFromJsonAsync<OfferDto>();
        Assert.NotNull(offer);
        Assert.Equal(merchant.Id, offer.MerchantId);
        Assert.Equal(75m, offer.DiscountedPrice);
        Assert.Equal($"/api/v1/offers/{offer.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CreateDraft_IsPrivateAndDoesNotNotify()
    {
        factory.FakeClock.Set(Now);
        var (token, userId) = CreateUserToken(true);
        var merchant = Comercio.Create(userId, "Tienda Sensus", "Calle Uno 1", "03100", 19.43, -99.13).Value;
        factory.ComercioRepository.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(merchant);
        factory.ComercioRepository.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        var notifier = Substitute.For<INearbyOfferNotifier>();
        using var draftFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices((_, services) =>
            services.Replace(ServiceDescriptor.Singleton(notifier))));
        using var client = draftFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var creation = await client.PostAsJsonAsync("/api/v1/offers", ValidOffer with { SaveAsDraft = true });

        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var dto = await creation.Content.ReadFromJsonAsync<OfferDto>();
        Assert.NotNull(dto);
        Assert.Equal("Draft", dto.Status);
        var draft = Assert.Single(factory.OfferRepository.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "AddAsync")
            .Select(call => (Offer)call.GetArguments()[0]!),
            offer => offer.Id == dto.Id);
        factory.OfferRepository.GetByIdAsync(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);
        factory.OfferRepository.GetByMerchantIdPagedAsync(
                merchant.Id, OfferStatus.Draft, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<Offer> { draft }, 1));

        using var detail = await client.GetAsync($"/api/v1/offers/{draft.Id}");
        using var inventory = await client.GetAsync("/api/v1/merchants/my-offers?status=Draft");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);
        var page = await inventory.Content.ReadFromJsonAsync<PagedResult<MerchantOfferDto>>();
        Assert.NotNull(page);
        Assert.Equal(dto.Id, Assert.Single(page.Items).Id);
        await notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateOffer_ReturnsConflict_WhenMerchantHas20ActiveOffers()
    {
        factory.FakeClock.Set(Now);
        var (token, userId) = CreateUserToken(true);
        var merchant = Comercio.Create(userId, "Tienda Límite", "Calle Dos 2", "03100", 19.43, -99.13).Value;
        factory.ComercioRepository.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(merchant);
        factory.OfferRepository.CountActiveByMerchantAsync(merchant.Id, Now, Arg.Any<CancellationToken>()).Returns(20L);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync("/api/v1/offers", ValidOffer);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateOffer_Returns202AcceptedAndHidesPublicly_WhenModerationUnavailable()
    {
        factory.FakeClock.Set(Now);
        var (merchantToken, userId) = CreateUserToken(true);
        var (otherToken, _) = CreateUserToken(false);
        var merchant = Comercio.Create(userId, "Tienda Pendiente", "Calle Tres 3", "03100", 19.43, -99.13).Value;
        factory.ComercioRepository.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(merchant);
        factory.ComercioRepository.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);

        var notifier = Substitute.For<INearbyOfferNotifier>();
        var unavailableModeration = Substitute.For<ITextModerationService>();
        unavailableModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TextModerationResult>(new HttpRequestException("OpenAI timeout")));

        using var pendingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices((_, services) =>
        {
            services.Replace(ServiceDescriptor.Singleton(notifier));
            services.Replace(ServiceDescriptor.Singleton(unavailableModeration));
        }));
        using var client = pendingFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);

        using var response = await client.PostAsJsonAsync("/api/v1/offers", ValidOffer);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OfferDto>();
        Assert.NotNull(dto);
        Assert.Equal(OfferStatus.PendingModeration.ToString(), dto.Status);
        Assert.Equal($"/api/v1/offers/{dto.Id}", response.Headers.Location?.OriginalString);

        Offer saved = Assert.Single(
            factory.OfferRepository.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "AddAsync")
                .Select(c => (Offer)c.GetArguments()[0]!),
            o => o.Id == dto.Id);
        factory.OfferRepository.GetByIdAsync(saved.Id, Arg.Any<CancellationToken>()).Returns(saved);

        // Otro usuario recibe 404 en el detalle público
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var publicDetail = await client.GetAsync($"/api/v1/offers/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, publicDetail.StatusCode);

        await notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default);
    }

    [Fact]
    public async Task CreateOffer_Returns201Created_WhenImageApprovedWithMobileDashContextFingerprint()
    {
        factory.FakeClock.Set(Now);
        var (token, userId) = CreateUserToken(true);
        var merchant = Comercio.Create(userId, "Heladería Centro", "Calle Cuatro 4", "03100", 19.43, -99.13).Value;
        factory.ComercioRepository.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(merchant);

        const string imageUrl = "https://res.cloudinary.com/nchjiueb/image/upload/v1/conos.jpg";
        var offerWithImage = ValidOffer with
        {
            Title = "Conos de helado 2x1",
            Description = "Promoción de conos artesanales",
            ImageUrl = imageUrl
        };
        var mobileFingerprint = Sensus.Application.Common.Moderation.ModeratedTextFingerprint.Create(
            $"{offerWithImage.Title} - {offerWithImage.Description}");
        factory.MediaAssetRepository.HasApprovedImageAsync(
                userId, imageUrl, mobileFingerprint, Arg.Any<CancellationToken>())
            .Returns(true);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync("/api/v1/offers", offerWithImage);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private (string Token, Guid UserId) CreateUserToken(bool merchant)
    {
        var user = User.Create("Persona de prueba", $"offer-{Guid.NewGuid():N}@example.test", "hash", true, Now).Value;
        if (merchant)
        {
            user.AddRole(RoleType.Comerciante);
        }

        factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return (token, user.Id);
    }
}
