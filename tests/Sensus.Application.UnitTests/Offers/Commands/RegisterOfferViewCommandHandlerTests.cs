using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.RegisterOfferView;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class RegisterOfferViewCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IOfferViewRepository _offerViewRepository = Substitute.For<IOfferViewRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly RegisterOfferViewCommandHandler _handler;

    public RegisterOfferViewCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(UserId.ToString());
        _dateTimeProvider.UtcNow.Returns(Now);

        _handler = new RegisterOfferViewCommandHandler(
            _offerRepository,
            _offerViewRepository,
            _currentUserService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldIncrementCounter_WhenViewIsNew()
    {
        // Arrange
        Offer offer = BuildOffer(Now);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerViewRepository.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);
        _offerRepository.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _offerRepository.Received(1).IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>());
        await _offerViewRepository.Received(1).TryRegisterAsync(
            Arg.Is<OfferView>(v => v.UserId == UserId && v.OfferId == offer.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotIncrementCounter_WhenViewAlreadyExistsForTheDay()
    {
        // Arrange
        Offer offer = BuildOffer(Now);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerViewRepository.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _offerRepository.DidNotReceiveWithAnyArgs()
            .IncrementViewCountAsync(default, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferIsArchived()
    {
        // Arrange
        Offer offer = BuildOffer(Now);
        offer.Archive(Now);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
        await _offerViewRepository.DidNotReceiveWithAnyArgs()
            .TryRegisterAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferDoesNotExist()
    {
        // Arrange
        _offerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Offer?)null);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferView.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCounterCannotBeIncremented()
    {
        // Arrange
        Offer offer = BuildOffer(Now);
        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _offerViewRepository.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);
        _offerRepository.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    private static Offer BuildOffer(DateTime now) =>
        Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            now.AddHours(-1),
            now.AddDays(1),
            null,
            -12.05,
            -77.04,
            now).Value;
}
