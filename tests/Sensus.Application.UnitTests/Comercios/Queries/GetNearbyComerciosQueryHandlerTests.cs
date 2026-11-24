using FluentAssertions;
using NSubstitute;
using Sensus.Application.Comercios.Queries.GetNearbyComercios;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Comercios.Queries;

public class GetNearbyComerciosQueryHandlerTests
{
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly GetNearbyComerciosQueryHandler _handler;

    public GetNearbyComerciosQueryHandlerTests()
    {
        _handler = new GetNearbyComerciosQueryHandler(_comercioRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldCapRadiusTo2000_WhenCallerIsGuest()
    {
        // Arrange
        _currentUserService.IsGuest.Returns(true);
        _comercioRepository.GetNearbyAsync(19.4326, -99.1332, 2000, 1, 20, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Comercio>(), 0));

        var query = new GetNearbyComerciosQuery(19.4326, -99.1332, RadioMetros: 5000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _comercioRepository.Received(1).GetNearbyAsync(19.4326, -99.1332, 2000, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAllowRequestedRadius_WhenCallerIsRegisteredUser()
    {
        // Arrange
        _currentUserService.IsGuest.Returns(false);
        _comercioRepository.GetNearbyAsync(19.4326, -99.1332, 5000, 1, 20, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Comercio>(), 0));

        var query = new GetNearbyComerciosQuery(19.4326, -99.1332, RadioMetros: 5000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _comercioRepository.Received(1).GetNearbyAsync(19.4326, -99.1332, 5000, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPopulateRatingSummaries_WhenStoreReviewRepositoryProvided()
    {
        // Arrange
        var storeReviewRepo = Substitute.For<IStoreReviewRepository>();
        var handler = new GetNearbyComerciosQueryHandler(_comercioRepository, _currentUserService, storeReviewRepo);

        var c1 = Comercio.Create(Guid.NewGuid(), "Comercio 1", "Calle 1", "06000", 19.43, -99.13).Value;
        var c2 = Comercio.Create(Guid.NewGuid(), "Comercio 2", "Calle 2", "06000", 19.44, -99.14).Value;
        _comercioRepository.GetNearbyAsync(19.43, -99.13, 2000, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<Comercio> { c1, c2 }, 2));

        var dict = new Dictionary<Guid, (double AverageRating, int ReviewCount)>
        {
            [c1.Id] = (4.65, 12)
        };
        storeReviewRepo.GetRatingSummariesByMerchantIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(dict);

        // Act
        var result = await handler.Handle(new GetNearbyComerciosQuery(19.43, -99.13), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.First(x => x.Id == c1.Id).AverageRating.Should().Be(4.6);
        result.Value.Items.First(x => x.Id == c1.Id).ReviewCount.Should().Be(12);
        result.Value.Items.First(x => x.Id == c2.Id).ReviewCount.Should().Be(0);
    }
}
