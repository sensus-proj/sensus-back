using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.ChangeOfferStatus;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class ChangeOfferStatusCommandHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ChangeOfferStatusCommandHandler> _logger;
    private readonly ChangeOfferStatusCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    public ChangeOfferStatusCommandHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _logger = Substitute.For<ILogger<ChangeOfferStatusCommandHandler>>();

        _dateTimeProvider.UtcNow.Returns(_now);
        _currentUserService.UserId.Returns(_userId.ToString());

        _handler = new ChangeOfferStatusCommandHandler(
            _offerRepository,
            _offerClaimRepository,
            _comercioRepository,
            _currentUserService,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new ChangeOfferStatusCommand(Guid.NewGuid(), "Paused");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenMerchantDoesNotExist()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var command = new ChangeOfferStatusCommand(Guid.NewGuid(), "Paused");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferNotFound()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offerId = Guid.NewGuid();
        _offerRepository.GetByIdAsync(offerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(null));

        var command = new ChangeOfferStatusCommand(offerId, "Paused");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenOfferBelongsToAnotherMerchant()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(), // different merchant
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new ChangeOfferStatusCommand(offer.Id, "Paused");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Forbidden");
    }

    [Fact]
    public async Task Handle_ShouldPausePublishedOffer_Successfully()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new ChangeOfferStatusCommand(offer.Id, "Paused");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Paused");
        await _offerRepository.Received(1).UpdateAsync(offer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldResumePausedOffer_Successfully()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Paused,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new ChangeOfferStatusCommand(offer.Id, "Published");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Published");
        await _offerRepository.Received(1).UpdateAsync(offer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenArchivingOfferWithActiveClaims()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));

        var command = new ChangeOfferStatusCommand(offer.Id, "Archived");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ActiveClaimsExist");
        await _offerRepository.DidNotReceive().UpdateAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldArchiveOffer_WhenNoActiveClaims()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        var command = new ChangeOfferStatusCommand(offer.Id, "Archived");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Archived");
        await _offerRepository.Received(1).UpdateAsync(offer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTransitioningFromArchived()
    {
        var comercio = Comercio.Create(_userId, "Comercio", "Calle 1", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Desc",
            100m,
            10,
            90m,
            10,
            _now,
            _now.AddDays(2),
            OfferStatus.Archived,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new ChangeOfferStatusCommand(offer.Id, "Published");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CannotTransitionFromArchived");
    }
}
