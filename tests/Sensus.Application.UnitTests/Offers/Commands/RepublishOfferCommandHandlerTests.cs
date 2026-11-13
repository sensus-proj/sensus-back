using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.RepublishOffer;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class RepublishOfferCommandHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RepublishOfferCommandHandler> _logger;
    private readonly RepublishOfferCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _comercioId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    public RepublishOfferCommandHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _logger = Substitute.For<ILogger<RepublishOfferCommandHandler>>();

        _dateTimeProvider.UtcNow.Returns(_now);
        _currentUserService.UserId.Returns(_userId.ToString());

        _handler = new RepublishOfferCommandHandler(
            _offerRepository,
            _comercioRepository,
            _currentUserService,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsEmpty()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new RepublishOfferCommand(Guid.NewGuid(), _now.AddHours(1), _now.AddDays(2), 10);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenMerchantDoesNotExist()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var command = new RepublishOfferCommand(Guid.NewGuid(), _now.AddHours(1), _now.AddDays(2), 10);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOfferDoesNotExist()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offerId = Guid.NewGuid();
        _offerRepository.GetByIdAsync(offerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(null));

        var command = new RepublishOfferCommand(offerId, _now.AddHours(1), _now.AddDays(2), 10);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenOfferBelongsToAnotherMerchant()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var otherMerchantId = Guid.NewGuid();
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            otherMerchantId,
            "Oferta ajena",
            "Desc",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new RepublishOfferCommand(offer.Id, _now.AddHours(1), _now.AddDays(2), 10);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Forbidden");
    }

    [Fact]
    public async Task Handle_ShouldRepublishOffer_WhenValid()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta pasada",
            "Desc",
            100m,
            20,
            80m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var newStartsAt = _now.AddHours(1);
        var newEndsAt = _now.AddDays(3);
        var command = new RepublishOfferCommand(offer.Id, newStartsAt, newEndsAt, 25);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OfferStatus.Published.ToString());
        result.Value.Stock.Should().Be(25);
        result.Value.StartsAt.Should().Be(newStartsAt);
        result.Value.EndsAt.Should().Be(newEndsAt);

        await _offerRepository.Received(1).UpdateAsync(offer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRepublishFailsDomainValidation()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        // Oferta actualmente publicada (no se puede republicar si está activa)
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta activa",
            "Desc",
            100m,
            20,
            80m,
            5,
            _now.AddHours(-1),
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now.AddHours(-1));

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = new RepublishOfferCommand(offer.Id, _now.AddHours(1), _now.AddDays(2), 10);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CannotRepublishActive");
        await _offerRepository.DidNotReceive().UpdateAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
    }
}
