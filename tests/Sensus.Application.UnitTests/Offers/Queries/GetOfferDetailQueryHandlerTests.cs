using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Queries.GetOfferDetail;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Queries;

public class GetOfferDetailQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly IOfferClaimRepository _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly GetOfferDetailQueryHandler _handler;

    public GetOfferDetailQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(UserId.ToString());

        _handler = new GetOfferDetailQueryHandler(
            _offerRepository,
            _comercioRepository,
            _offerClaimRepository,
            _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnDetailWithMerchantData_WhenOfferIsVisible()
    {
        // Arrange
        Offer offer = BuildOffer(viewCount: 7);
        Comercio comercio = BuildComercio(offer.MerchantId);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(comercio);
        _offerClaimRepository
            .GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantName.Should().Be("Restaurante Central");
        result.Value.MerchantAddress.Should().Be("Calle Reforma 100");
        result.Value.Category.Should().Be(MerchantCategory.Restaurantes.ToString());
        result.Value.ViewCount.Should().Be(7);
        result.Value.HasActiveClaim.Should().BeFalse();
        result.Value.DiscountedPrice.Should().Be(offer.DiscountedPrice);
    }

    [Fact]
    public async Task Handle_ShouldFlagActiveClaim_WhenUserHasOne()
    {
        // Arrange
        Offer offer = BuildOffer();
        Comercio comercio = BuildComercio(offer.MerchantId);
        OfferClaim claim = OfferClaim.Create(UserId, offer.Id, offer.MerchantId, Now).Value;

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(comercio);
        _offerClaimRepository
            .GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns(claim);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasActiveClaim.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferIsArchived()
    {
        // Arrange
        Offer offer = BuildOffer();
        offer.Archive(Now);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
        await _comercioRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferDoesNotExist()
    {
        // Arrange
        _offerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Offer?)null);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenMerchantIsInactive()
    {
        // Arrange
        Offer offer = BuildOffer();
        Comercio comercio = BuildComercio(offer.MerchantId);
        comercio.Deactivate();

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenMerchantDoesNotExist()
    {
        // Arrange
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns((Comercio?)null);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldNotQueryClaims_WhenUserIsAnonymous()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        Offer offer = BuildOffer();
        Comercio comercio = BuildComercio(offer.MerchantId);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasActiveClaim.Should().BeFalse();
        await _offerClaimRepository.DidNotReceiveWithAnyArgs()
            .GetActiveByUserAndOfferAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_ShouldStoreDetailWithOffersEntityTtl()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        var handler = new GetOfferDetailQueryHandler(_offerRepository, _comercioRepository, _offerClaimRepository, _currentUserService, cache);
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(BuildComercio(offer.MerchantId));
        cache.GetAsync<Sensus.Application.Offers.DTOs.OfferDetailDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Sensus.Application.Offers.DTOs.OfferDetailDto?)null);

        var result = await handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await cache.Received(1).SetForEntityAsync(
            $"sensus:offers:detail:{offer.Id}",
            Arg.Any<Sensus.Application.Offers.DTOs.OfferDetailDto>(),
            "Offers",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ShouldNotQueryRepositories()
    {
        ICacheService cache = Substitute.For<ICacheService>();
        var handler = new GetOfferDetailQueryHandler(_offerRepository, _comercioRepository, _offerClaimRepository, _currentUserService, cache);
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(BuildComercio(offer.MerchantId));

        Sensus.Application.Offers.DTOs.OfferDetailDto? captured = null;
        cache.GetAsync<Sensus.Application.Offers.DTOs.OfferDetailDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => captured);
        await cache.SetForEntityAsync(Arg.Any<string>(), Arg.Do<Sensus.Application.Offers.DTOs.OfferDetailDto>(d => captured = d), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);
        _offerRepository.ClearReceivedCalls();

        var second = await handler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        await _offerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Offer BuildOffer(int viewCount = 0)
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            Now.AddHours(-1),
            Now.AddDays(1),
            null,
            19.4326,
            -99.1332,
            Now).Value;

        return Offer.Reconstitute(
            offer.Id,
            offer.MerchantId,
            offer.Title,
            offer.Description,
            offer.OriginalPrice,
            offer.DiscountPercentage,
            offer.DiscountedPrice,
            offer.Stock,
            offer.StartsAt,
            offer.EndsAt,
            offer.Status,
            offer.ImageUrl,
            offer.Latitude,
            offer.Longitude,
            offer.CreatedAtUtc,
            offer.LastModifiedAtUtc,
            viewCount);
    }

    private static Comercio BuildComercio(Guid comercioId)
    {
        return Comercio.Reconstitute(
            comercioId,
            Guid.NewGuid(),
            "Restaurante Central",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332,
            activo: true,
            Now,
            MerchantCategory.Restaurantes);
    }
}
