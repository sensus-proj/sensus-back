using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.ToggleOfferFavorite;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

public sealed class ToggleOfferFavoriteCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IOfferFavoriteRepository _favoriteRepository = Substitute.For<IOfferFavoriteRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();

    public ToggleOfferFavoriteCommandHandlerTests()
    {
        _currentUser.UserId.Returns(UserId.ToString());
        _currentUser.IsGuest.Returns(false);
        _clock.UtcNow.Returns(Now);
    }

    [Fact]
    public async Task Handle_TogglesOn_AndInvalidatesAffinityCache()
    {
        Offer offer = ActiveOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _favoriteRepository.ToggleFavoriteAsync(UserId, offer.Id, Now, Arg.Any<CancellationToken>()).Returns(true);
        _favoriteRepository.CountByOfferIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(7);

        Result<FavoriteToggleResultDto> result = await Handler().Handle(
            new ToggleOfferFavoriteCommand(offer.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Favorited.Should().BeTrue();
        result.Value.FavoriteCount.Should().Be(7);
        await _cacheService.Received(1).RemoveAsync(OfferCacheKeys.Detail(offer.Id), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(FeedCacheKeys.Affinity(UserId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TogglesOff_AndReturnsUpdatedCount()
    {
        Offer offer = ActiveOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _favoriteRepository.ToggleFavoriteAsync(UserId, offer.Id, Now, Arg.Any<CancellationToken>()).Returns(false);
        _favoriteRepository.CountByOfferIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(0);

        Result<FavoriteToggleResultDto> result = await Handler().Handle(
            new ToggleOfferFavoriteCommand(offer.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Favorited.Should().BeFalse();
        result.Value.FavoriteCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenUserIsGuest()
    {
        _currentUser.IsGuest.Returns(true);

        Result<FavoriteToggleResultDto> result = await Handler().Handle(
            new ToggleOfferFavoriteCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.GuestForbidden");
        await _offerRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        Result<FavoriteToggleResultDto> result = await Handler().Handle(
            new ToggleOfferFavoriteCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.Unauthorized");
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenOfferIsArchived()
    {
        Offer offer = ArchivedOffer();
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        Result<FavoriteToggleResultDto> result = await Handler().Handle(
            new ToggleOfferFavoriteCommand(offer.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
        await _favoriteRepository.DidNotReceiveWithAnyArgs().ToggleFavoriteAsync(default, default, default, default);
    }

    private ToggleOfferFavoriteCommandHandler Handler() =>
        new(
            _offerRepository,
            _favoriteRepository,
            _currentUser,
            _clock,
            NullLogger<ToggleOfferFavoriteCommandHandler>.Instance,
            _cacheService);

    private static Offer ActiveOffer() => Offer.Create(
        Guid.NewGuid(),
        "Oferta activa",
        "Descripción de la oferta activa",
        100m,
        20,
        5,
        Now.AddDays(-1),
        Now.AddDays(1),
        nowUtc: Now,
        category: "Food").Value;

    private static Offer ArchivedOffer() => Offer.Reconstitute(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Oferta archivada",
        "Descripción de la oferta archivada",
        100m,
        20,
        80m,
        5,
        Now.AddDays(-5),
        Now.AddDays(-1),
        OfferStatus.Archived,
        null,
        null,
        null,
        Now.AddDays(-10),
        category: "Food");
}
