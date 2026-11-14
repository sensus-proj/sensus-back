using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Queries.GetNearbyOffers;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Queries;

public class GetNearbyOffersQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetNearbyOffersQueryHandler _handler;

    public GetNearbyOffersQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new GetNearbyOffersQueryHandler(
            _offerRepository,
            _currentUserService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldPassResolvedRadiusAndCategory_ForRegisteredUser()
    {
        // Arrange
        _currentUserService.IsGuest.Returns(false);
        NearbyOfferHit hit = BuildHit();
        _offerRepository.GetNearbyAsync(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<MerchantCategory?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new NearbyOffersPage([hit], 1));

        var query = new GetNearbyOffersQuery(-12.05, -77.04, 3, "Restaurantes", 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].DistanceMeters.Should().Be(150.5);
        result.Value.Items[0].Category.Should().Be("Restaurantes");
        result.Value.TotalCount.Should().Be(1);

        await _offerRepository.Received(1).GetNearbyAsync(
            -12.05,
            -77.04,
            3000,
            MerchantCategory.Restaurantes,
            1,
            20,
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldClampGuestRadiusToTwoKilometers()
    {
        // Arrange
        _currentUserService.IsGuest.Returns(true);
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

        var query = new GetNearbyOffersQuery(-12.05, -77.04, 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _offerRepository.Received(1).GetNearbyAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            2000,
            Arg.Any<MerchantCategory?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(6)]
    public async Task Handle_ShouldRejectOutOfRangeRadius_ForRegisteredUser(double radiusKm)
    {
        // Arrange
        _currentUserService.IsGuest.Returns(false);
        var query = new GetNearbyOffersQuery(-12.05, -77.04, radiusKm);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.RadiusOutOfRange");
        await _offerRepository.DidNotReceiveWithAnyArgs().GetNearbyAsync(
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default);
    }

    [Fact]
    public async Task Handle_ShouldRejectUnknownCategory()
    {
        // Arrange
        var query = new GetNearbyOffersQuery(-12.05, -77.04, 3, "NoExiste");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CategoryInvalid");
    }

    private static NearbyOfferHit BuildHit()
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
            -12.05,
            -77.04,
            Now).Value;

        return new NearbyOfferHit(offer, 150.5, "Restaurante Central", MerchantCategory.Restaurantes);
    }
}
