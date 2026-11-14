using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.UpdateOffer;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class UpdateOfferCommandHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateOfferCommandHandler> _logger;
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly UpdateOfferCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    public UpdateOfferCommandHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _logger = Substitute.For<ILogger<UpdateOfferCommandHandler>>();

        _dateTimeProvider.UtcNow.Returns(_now);
        _currentUserService.UserId.Returns(_userId.ToString());

        _handler = new UpdateOfferCommandHandler(
            _offerRepository,
            _offerClaimRepository,
            _comercioRepository,
            _currentUserService,
            _dateTimeProvider,
            _logger,
            _cacheService);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsEmpty()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = CreateValidCommand(Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenMerchantDoesNotExist()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var command = CreateValidCommand(Guid.NewGuid());
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

        var command = CreateValidCommand(offerId);
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
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now,
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        var command = CreateValidCommand(offer.Id);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Forbidden");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenStockIsLessThanActiveClaims()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Descripción original",
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
            _now,
            version: 1);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(5));

        // Intenta bajar stock a 3 cuando hay 5 apartados activos
        var command = new UpdateOfferCommand(
            offer.Id,
            "Oferta Actualizada",
            "Descripción válida",
            100m,
            10,
            Stock: 3,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            ExpectedVersion: 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.StockBelowActiveClaims");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenVersionMismatch()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Descripción original",
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
            _now,
            version: 2); // Oferta ya en versión 2

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        // Cliente envía versión 1 esperada
        var command = new UpdateOfferCommand(
            offer.Id,
            "Oferta Actualizada",
            "Descripción válida",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            ExpectedVersion: 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.VersionConflict");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenOptimisticLockFailsInRepository()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta",
            "Descripción original",
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
            _now,
            version: 1);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        _offerRepository.UpdateWithVersionCheckAsync(Arg.Any<Offer>(), 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var command = CreateValidCommand(offer.Id, expectedVersion: 1);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.VersionConflict");
    }

    [Fact]
    public async Task Handle_ShouldSucceedAndIncrementVersion_WhenValid()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Título original",
            "Descripción original",
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
            _now,
            version: 1);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Offer?>(offer));

        _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        _offerRepository.UpdateWithVersionCheckAsync(Arg.Any<Offer>(), 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var command = new UpdateOfferCommand(
            offer.Id,
            "Título nuevo y fresco",
            "Descripción nueva y detallada",
            150m,
            20,
            Stock: 8,
            StartsAt: _now.AddHours(2),
            EndsAt: _now.AddDays(3),
            ImageUrl: "https://cloudinary.com/new.jpg",
            ExpectedVersion: 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Título nuevo y fresco");
        result.Value.OriginalPrice.Should().Be(150m);
        result.Value.DiscountPercentage.Should().Be(20);
        result.Value.DiscountedPrice.Should().Be(120m);
        result.Value.Stock.Should().Be(8);
        result.Value.Version.Should().Be(2);

        await _offerRepository.Received(1)
            .UpdateWithVersionCheckAsync(Arg.Is<Offer>(o => o.Version == 2), 1, Arg.Any<CancellationToken>());

        await _cacheService.Received(1).RemoveAsync(OfferCacheKeys.Detail(offer.Id), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    private UpdateOfferCommand CreateValidCommand(Guid offerId, int expectedVersion = 1) =>
        new(
            offerId,
            "Título válido",
            "Descripción válida",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            expectedVersion);
}
