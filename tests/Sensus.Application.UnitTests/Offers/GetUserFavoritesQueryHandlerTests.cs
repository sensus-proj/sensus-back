using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetUserFavorites;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

public sealed class GetUserFavoritesQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly IOfferFavoriteRepository _favoriteRepository = Substitute.For<IOfferFavoriteRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IOfferLikeRepository _offerLikeRepository = Substitute.For<IOfferLikeRepository>();

    public GetUserFavoritesQueryHandlerTests()
    {
        _currentUser.UserId.Returns(UserId.ToString());
        _currentUser.IsGuest.Returns(false);
        _clock.UtcNow.Returns(Now);
    }

    [Fact]
    public async Task Handle_ReturnsPagedFavoritesAndEnrichesLikeCounts()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(),
            "Pan artesanal",
            "Masa madre del día",
            60m,
            25,
            4,
            Now.AddDays(-1),
            Now.AddDays(2),
            nowUtc: Now,
            category: "Bakery").Value;

        var entry = new FavoriteOfferEntry(offer, "Panadería Central", MerchantCategory.Cafeterias, Now.AddMinutes(-15));
        _favoriteRepository
            .GetPagedByUserIdAsync(UserId, 1, 20, Now, Arg.Any<CancellationToken>())
            .Returns(new FavoriteOffersPage([entry], 1));
        _offerLikeRepository
            .CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [offer.Id] = 9 });

        Result<PagedResult<FavoriteOfferDto>> result = await Handler().Handle(
            new GetUserFavoritesQuery(1, 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        FavoriteOfferDto item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(offer.Id);
        item.MerchantName.Should().Be("Panadería Central");
        item.LikesCount.Should().Be(9);
        item.FavoritedAtUtc.Should().Be(Now.AddMinutes(-15));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyPage_WhenUserHasNoFavorites()
    {
        _favoriteRepository
            .GetPagedByUserIdAsync(UserId, 2, 10, Now, Arg.Any<CancellationToken>())
            .Returns(new FavoriteOffersPage([], 0));

        Result<PagedResult<FavoriteOfferDto>> result = await Handler().Handle(
            new GetUserFavoritesQuery(2, 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenUserIsGuest()
    {
        _currentUser.IsGuest.Returns(true);

        Result<PagedResult<FavoriteOfferDto>> result = await Handler().Handle(
            new GetUserFavoritesQuery(1, 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.GuestForbidden");
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenUserIdIsMissing()
    {
        _currentUser.UserId.Returns((string?)null);

        Result<PagedResult<FavoriteOfferDto>> result = await Handler().Handle(
            new GetUserFavoritesQuery(1, 20),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.Unauthorized");
    }

    [Fact]
    public void Validator_RejectsInvalidPageAndPageSize()
    {
        var validator = new GetUserFavoritesQueryValidator();

        validator.Validate(new GetUserFavoritesQuery(0, 20)).IsValid.Should().BeFalse();
        validator.Validate(new GetUserFavoritesQuery(1, 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetUserFavoritesQuery(1, 101)).IsValid.Should().BeFalse();
        validator.Validate(new GetUserFavoritesQuery(1, 20)).IsValid.Should().BeTrue();
        validator.Validate(new GetUserFavoritesQuery(2, 100)).IsValid.Should().BeTrue();
    }

    private GetUserFavoritesQueryHandler Handler() =>
        new(_favoriteRepository, _currentUser, _clock, _offerLikeRepository);
}
