using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetNearbyOffers;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

/// <summary>
/// Pruebas del caché de ofertas (Spec 048: FR-048-05, FR-048-07 y FR-048-11).
/// </summary>
public class OfferCacheTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();

    public OfferCacheTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _currentUserService.IsGuest.Returns(false);
    }

    [Fact]
    public void Nearby_ShouldBuildKeyWithInvariantCultureAndNormalizedCategory()
    {
        string key = OfferCacheKeys.Nearby(-12.0500001, -77.04, 3000, " restaurantes ", 2, 20);

        key.Should().Be("sensus:offers:nearby:-12.05000:-77.04000:3000:RESTAURANTES:2:20");
        key.Should().StartWith(OfferCacheKeys.NearbyPrefix);
    }

    [Fact]
    public void Nearby_ShouldUseDashWhenCategoryIsMissing()
    {
        OfferCacheKeys.Nearby(1, 2, 2000, null, 1, 20)
            .Should().Be("sensus:offers:nearby:1.00000:2.00000:2000:-:1:20");
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveDetailAndNearbyEntries_WhenOfferIdIsGiven()
    {
        Guid offerId = Guid.NewGuid();

        await _cacheService.InvalidateAsync(offerId, CancellationToken.None);

        await _cacheService.Received(1).RemoveAsync(OfferCacheKeys.Detail(offerId), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ShouldOnlyRemoveNearbyEntries_WhenOfferIdIsMissing()
    {
        await _cacheService.InvalidateAsync(null, CancellationToken.None);

        await _cacheService.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ShouldDoNothing_WhenCacheIsNotRegistered()
    {
        ICacheService? cache = null;

        Func<Task> act = () => cache.InvalidateAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NearbyHandler_ShouldReturnCachedPage_WithoutQueryingRepository()
    {
        var cached = new PagedResult<NearbyOfferDto>([], 0, 1, 20);
        string key = OfferCacheKeys.Nearby(-12.05, -77.04, 3000, null, 1, 20);
        _cacheService.GetAsync<PagedResult<NearbyOfferDto>>(key, Arg.Any<CancellationToken>()).Returns(cached);

        var result = await CreateHandler(_cacheService)
            .Handle(new GetNearbyOffersQuery(-12.05, -77.04, 3, null, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);
        await _offerRepository.DidNotReceiveWithAnyArgs().GetNearbyAsync(
            default, default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task NearbyHandler_ShouldQueryRepositoryAndStoreResult_OnCacheMiss()
    {
        string key = OfferCacheKeys.Nearby(-12.05, -77.04, 3000, null, 1, 20);
        _cacheService.GetAsync<PagedResult<NearbyOfferDto>>(key, Arg.Any<CancellationToken>())
            .Returns((PagedResult<NearbyOfferDto>?)null);
        StubEmptyRepositoryPage();

        var result = await CreateHandler(_cacheService)
            .Handle(new GetNearbyOffersQuery(-12.05, -77.04, 3, null, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _cacheService.Received(1).SetForEntityAsync(
            key,
            Arg.Any<PagedResult<NearbyOfferDto>>(),
            OfferCacheKeys.EntityName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NearbyHandler_ShouldReadFromRepository_WhenCacheIsNotRegistered()
    {
        StubEmptyRepositoryPage();

        var result = await CreateHandler(cacheService: null)
            .Handle(new GetNearbyOffersQuery(-12.05, -77.04, 3, null, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _offerRepository.ReceivedWithAnyArgs(1).GetNearbyAsync(
            default, default, default, default, default, default, default, default);
    }

    private GetNearbyOffersQueryHandler CreateHandler(ICacheService? cacheService) =>
        new(_offerRepository, _currentUserService, _dateTimeProvider, cacheService);

    private void StubEmptyRepositoryPage()
    {
        _offerRepository.GetNearbyAsync(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<MerchantCategory?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new NearbyOffersPage([], 0));
    }
}
