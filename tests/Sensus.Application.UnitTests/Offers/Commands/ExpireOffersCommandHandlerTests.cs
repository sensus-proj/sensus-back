using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.ExpireOffers;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class ExpireOffersCommandHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ExpireOffersCommandHandler> _logger;
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ExpireOffersCommandHandler _handler;

    private readonly DateTime _testNow = new(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);

    public ExpireOffersCommandHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _logger = Substitute.For<ILogger<ExpireOffersCommandHandler>>();

        _dateTimeProvider.UtcNow.Returns(_testNow);

        _handler = new ExpireOffersCommandHandler(
            _offerRepository,
            _dateTimeProvider,
            _logger,
            _cacheService);
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryExpireOffersAsync_AndReturnCount()
    {
        // Arrange
        _offerRepository.ExpireOffersAsync(_testNow, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(7));

        var command = new ExpireOffersCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiredCount.Should().Be(7);
        result.Value.ExecutedAtUtc.Should().Be(_testNow);

        await _offerRepository.Received(1).ExpireOffersAsync(_testNow, Arg.Any<CancellationToken>());

        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
