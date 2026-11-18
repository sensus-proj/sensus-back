using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Ai.Services;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Ai;

/// <summary>FR-046-18: tarjetas de oferta con datos completos para las ofertas mencionadas en la respuesta.</summary>
public sealed class CitedOfferEnricherTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOfferRepository _offers = Substitute.For<IOfferRepository>();
    private readonly IComercioRepository _comercios = Substitute.For<IComercioRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly SensiSettings _settings = new() { MaxCitedOfferCards = 2 };

    public CitedOfferEnricherTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    private CitedOfferEnricher CreateSut() =>
        new(_offers, _comercios, _clock, Options.Create(_settings), NullLogger<CitedOfferEnricher>.Instance);

    private static CitedOfferDto Cited(Guid id, string title) =>
        new(id.ToString(), title, Guid.NewGuid().ToString(), string.Empty, 10m, 12m, 1, true);

    private static readonly IReadOnlySet<string> NoExclusions = new HashSet<string>();

    [Fact]
    public async Task EnrichAsync_WhenNothingCited_ReturnsEmptyWithoutQueryingRepositories()
    {
        var result = await CreateSut().EnrichAsync([], "Hola", NoExclusions, null, null);

        result.Should().BeEmpty();
        await _offers.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task EnrichAsync_WhenReplyDoesNotMentionTitle_ReturnsEmpty()
    {
        var cited = Cited(Guid.NewGuid(), "Teclado Gaming");

        var result = await CreateSut().EnrichAsync([cited], "No encontré nada.", NoExclusions, null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_WhenCitedOfferIsExcluded_SkipsIt()
    {
        var cited = Cited(Guid.NewGuid(), "Teclado Gaming");
        var excluded = new HashSet<string> { cited.OfferId };

        var result = await CreateSut().EnrichAsync([cited], "Mira el teclado gaming", excluded, null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_WhenInvalidOfferId_KeepsBasicData()
    {
        var cited = new CitedOfferDto("no-guid", "Teclado Gaming", "m1", "Tienda", 10m, 12m, 1, true);

        var result = await CreateSut().EnrichAsync([cited], "El Teclado Gaming esta barato", NoExclusions, null, null);

        result.Should().ContainSingle().Which.Should().Be(cited);
    }

    [Fact]
    public async Task EnrichAsync_WhenOfferNotFound_KeepsBasicData()
    {
        var cited = Cited(Guid.NewGuid(), "Teclado Gaming");
        _offers.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Offer?)null);

        var result = await CreateSut().EnrichAsync([cited], "El Teclado Gaming esta barato", NoExclusions, null, null);

        result.Should().ContainSingle().Which.Should().Be(cited);
    }

    [Fact]
    public async Task EnrichAsync_WhenRepositoryFails_KeepsBasicDataAndDoesNotThrow()
    {
        var cited = Cited(Guid.NewGuid(), "Teclado Gaming");
        _offers.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateSut().EnrichAsync([cited], "El Teclado Gaming esta barato", NoExclusions, null, null);

        result.Should().ContainSingle().Which.Should().Be(cited);
    }

    [Fact]
    public async Task EnrichAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cited = Cited(Guid.NewGuid(), "Teclado Gaming");
        _offers.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => CreateSut().EnrichAsync([cited], "El Teclado Gaming esta barato", NoExclusions, null, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnrichAsync_LimitsCardsToConfiguredMaximum()
    {
        var cited = Enumerable.Range(1, 4).Select(i => Cited(Guid.NewGuid(), $"Oferta {i}")).ToList();
        _offers.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Offer?)null);

        var result = await CreateSut().EnrichAsync(
            cited,
            "Oferta 1, Oferta 2, Oferta 3 y Oferta 4",
            NoExclusions,
            null,
            null);

        result.Should().HaveCount(2);
        result.Select(o => o.Title).Should().Equal("Oferta 1", "Oferta 2");
    }

    [Fact]
    public async Task EnrichAsync_FillsFullDataFromRepositoryAndComputesDistance()
    {
        var merchantId = Guid.NewGuid();
        var offer = Offer.Create(
                merchantId,
                "Teclado Gaming",
                "Teclado con RPG para más FPS",
                200m,
                25,
                1,
                Now.AddDays(-1),
                Now.AddDays(14),
                "https://img/teclado.jpg",
                19.4326,
                -99.1332,
                Now.AddDays(-2),
                "Tecnologia")
            .Value;
        var comercio = Comercio.Create(Guid.NewGuid(), "Tienda 3B", "Calle Falsa 123", "06000", 19.4326, -99.1332, Now)
            .Value;
        _offers.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercios.GetByIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns(comercio);

        var result = await CreateSut().EnrichAsync(
            [Cited(offer.Id, "Teclado Gaming")],
            "Encontré el teclado gaming por $150.",
            NoExclusions,
            19.4326,
            -99.1332);

        var card = result.Should().ContainSingle().Subject;
        card.Description.Should().Be("Teclado con RPG para más FPS");
        card.DiscountPercentage.Should().Be(25);
        card.OfferPriceMxn.Should().Be(150m);
        card.RegularPriceMxn.Should().Be(200m);
        card.AvailableStock.Should().Be(1);
        card.ImageUrl.Should().Be("https://img/teclado.jpg");
        card.Category.Should().Be("Tecnologia");
        card.EndsAtUtc.Should().Be(Now.AddDays(14));
        card.MerchantName.Should().Be("Tienda 3B");
        card.MerchantAddress.Should().Be("Calle Falsa 123");
        card.Latitude.Should().Be(19.4326);
        card.Longitude.Should().Be(-99.1332);
        card.IsOpenNow.Should().BeTrue();
        card.DistanceKm.Should().Be(0d);
    }

    [Fact]
    public async Task EnrichAsync_WithoutUserLocation_LeavesDistanceEmpty()
    {
        var merchantId = Guid.NewGuid();
        var offer = Offer.Create(merchantId, "Pan", "Pan fresco", 20m, 10, 5, Now.AddDays(-1), Now.AddDays(2), nowUtc: Now.AddDays(-2)).Value;
        _offers.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercios.GetByIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns((Comercio?)null);

        var result = await CreateSut().EnrichAsync([Cited(offer.Id, "Pan")], "Hay Pan disponible", NoExclusions, null, null);

        var card = result.Should().ContainSingle().Subject;
        card.DistanceKm.Should().BeNull();
        card.Latitude.Should().BeNull();
        card.MerchantAddress.Should().BeNull();
    }
}
