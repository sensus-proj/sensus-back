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
