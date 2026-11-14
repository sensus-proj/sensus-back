using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.ClaimOffer;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class ClaimOfferCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IOfferClaimRepository _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IClaimCodeService _claimCodeService = Substitute.For<IClaimCodeService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ClaimOfferCommandHandler _handler;

    public ClaimOfferCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(UserId.ToString());
        _dateTimeProvider.UtcNow.Returns(Now);
        _claimCodeService.Generate(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>())
            .Returns(call => new ClaimCode("v1.code", "hash", "v1.code"));

        _handler = new ClaimOfferCommandHandler(
            _offerRepository,
            _offerClaimRepository,
            _currentUserService,
            _claimCodeService,
            _dateTimeProvider,
            _cacheService);
    }

    [Fact]
    public async Task Handle_ShouldCreateClaimAndReserveStock_WhenOfferIsAvailable()
    {
        // Arrange
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerClaimRepository.GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _offerRepository.TryReserveStockAsync(offer.Id, Now, Arg.Any<CancellationToken>()).Returns(true);
        _offerClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OfferId.Should().Be(offer.Id);
        result.Value.Code.Should().Be("v1.code");
        result.Value.QrPayload.Should().Be("v1.code");
        result.Value.ExpiresAtUtc.Should().Be(Now.AddHours(24));

        await _offerClaimRepository.Received(1).TryAddAsync(
            Arg.Is<OfferClaim>(c => c.CodeHash == "hash" && c.UserId == UserId),
            Arg.Any<CancellationToken>());

        await _cacheService.Received(1).RemoveAsync(OfferCacheKeys.Detail(offer.Id), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenUserAlreadyHasActiveClaim()
    {
        // Arrange
        Offer offer = BuildOffer();
        OfferClaim existing = OfferClaim.Create(UserId, offer.Id, offer.MerchantId, Now).Value;
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerClaimRepository.GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.AlreadyActive");
        await _offerRepository.DidNotReceiveWithAnyArgs()
            .TryReserveStockAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenThereIsNoStock()
    {
        // Arrange
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerClaimRepository.GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _offerRepository.TryReserveStockAsync(offer.Id, Now, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.NoStock");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenOfferIsNotAvailable()
    {
        // Arrange
        _offerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Offer?)null);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.OfferNotAvailable");
    }

    [Fact]
    public async Task Handle_ShouldCompensateStock_WhenDuplicateClaimRejectsInsert()
    {
        // Arrange
        Offer offer = BuildOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerClaimRepository.GetActiveByUserAndOfferAsync(UserId, offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _offerRepository.TryReserveStockAsync(offer.Id, Now, Arg.Any<CancellationToken>()).Returns(true);
        _offerClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.AlreadyActive");
        await _offerRepository.Received(1).ReturnStockAsync(offer.Id, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        // Act
        var result = await _handler.Handle(new ClaimOfferCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.Unauthorized");
    }

    private static Offer BuildOffer()
    {
        return Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            Now.AddHours(-1),
            Now.AddDays(1),
            null,
            -12.05,
            -77.04,
            Now).Value;
    }
}
