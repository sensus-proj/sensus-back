using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.ExpireClaims;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class ExpireClaimsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OfferId = Guid.NewGuid();

    private readonly IOfferClaimRepository _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<ExpireClaimsCommandHandler> _logger =
        Substitute.For<ILogger<ExpireClaimsCommandHandler>>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ExpireClaimsCommandHandler _handler;

    public ExpireClaimsCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new ExpireClaimsCommandHandler(
            _offerClaimRepository,
            _offerRepository,
            _dateTimeProvider,
            _logger,
            _cacheService);
    }

    [Fact]
    public async Task Handle_ShouldReturnStockGroupedByOffer()
    {
        // Arrange
        OfferClaim first = BuildExpiredClaim();
        OfferClaim second = BuildExpiredClaim();
        _offerClaimRepository.ExpireClaimsAsync(Now, Arg.Any<CancellationToken>())
            .Returns(new List<OfferClaim> { first, second });
        _offerRepository.ReturnStockAsync(OfferId, 2, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(new ExpireClaimsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCount.Should().Be(2);
        result.Value.ReturnedUnits.Should().Be(2);
        await _offerRepository.Received(1).ReturnStockAsync(OfferId, 2, Arg.Any<CancellationToken>());

        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCountUnits_WhenStockCouldNotBeReturned()
    {
        // Arrange
        _offerClaimRepository.ExpireClaimsAsync(Now, Arg.Any<CancellationToken>())
            .Returns(new List<OfferClaim> { BuildExpiredClaim() });
        _offerRepository.ReturnStockAsync(OfferId, 1, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new ExpireClaimsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCount.Should().Be(1);
        result.Value.ReturnedUnits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNoClaimsExpired()
    {
        // Arrange
        _offerClaimRepository.ExpireClaimsAsync(Now, Arg.Any<CancellationToken>())
            .Returns(new List<OfferClaim>());

        // Act
        var result = await _handler.Handle(new ExpireClaimsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCount.Should().Be(0);
        result.Value.ReturnedUnits.Should().Be(0);

        await _cacheService.DidNotReceiveWithAnyArgs().RemoveByPrefixAsync(default!, default);
    }

    private static OfferClaim BuildExpiredClaim()
    {
        return OfferClaim.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            OfferId,
            Guid.NewGuid(),
            "hash",
            OfferClaimStatus.Expired,
            Now,
            null,
            Now.AddHours(-25));
    }
}
