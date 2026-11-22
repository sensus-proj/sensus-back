using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.CreateOffer;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class CreateOfferCommandHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateOfferCommandHandler> _logger;
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly CreateOfferCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    public CreateOfferCommandHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _logger = Substitute.For<ILogger<CreateOfferCommandHandler>>();

        _dateTimeProvider.UtcNow.Returns(_now);
        _currentUserService.UserId.Returns(_userId.ToString());

        _handler = new CreateOfferCommandHandler(
            _offerRepository,
            _comercioRepository,
            _currentUserService,
            _dateTimeProvider,
            _logger,
            _cacheService);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdNotSet()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new CreateOfferCommand(
            "Título", "Descripción", 100m, 10, 5, _now.AddHours(1), _now.AddDays(2));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNoMerchantExists()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var command = new CreateOfferCommand(
            "Título", "Descripción", 100m, 10, 5, _now.AddHours(1), _now.AddDays(2));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldCreateOfferAndPersist_WhenDataIsValid()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var command = new CreateOfferCommand(
            "Combo Pizza Grande",
            "Incluye refresco de 2L",
            200m,
            25,
            10,
            _now.AddHours(1),
            _now.AddDays(5),
            "https://cloudinary.com/pizza.jpg");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Combo Pizza Grande");
        result.Value.OriginalPrice.Should().Be(200m);
        result.Value.DiscountPercentage.Should().Be(25);
        result.Value.DiscountedPrice.Should().Be(150m);
        result.Value.Stock.Should().Be(10);
        result.Value.Status.Should().Be(OfferStatus.Published.ToString());

        await _offerRepository.Received(1).AddAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());

        await _cacheService.Received(1).RemoveAsync(Arg.Is<string>(k => k.StartsWith("sensus:offers:detail:")), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveByPrefixAsync(OfferCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenOfferCreateFailsDomainValidation()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        // Fecha de fin en el pasado respecto a _now
        var command = new CreateOfferCommand(
            "Combo Inválido",
            "Descripción válida",
            100m,
            10,
            5,
            _now.AddDays(-2),
            _now.AddDays(-1));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ExpiredEndDate");
        await _offerRepository.DidNotReceive().AddAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotifyNearbyUsersAfterPersisting()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));
        var notifier = Substitute.For<INearbyOfferNotifier>();
        var handler = new CreateOfferCommandHandler(
            _offerRepository, _comercioRepository, _currentUserService, _dateTimeProvider, _logger, _cacheService, notifier);
        var command = new CreateOfferCommand(
            "Detergente", "Detergente líquido de 3 litros", 100m, 20, 10, _now, _now.AddDays(5), null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _offerRepository.AddAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
            notifier.NotifyAsync(Arg.Is<Offer>(o => o.Id == result.Value.Id), _userId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_WhenStrictCategoryEnabled_ShouldRejectMissingOrInvalidAndAcceptValidCode()
    {
        var comercio = Comercio.Create(_userId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var strictHandler = new CreateOfferCommandHandler(
            _offerRepository,
            _comercioRepository,
            _currentUserService,
            _dateTimeProvider,
            _logger,
            _cacheService,
            categorySettings: Options.Create(new OfferCategorySettings { RequireStrictValidation = true }));

        var missingResult = await strictHandler.Handle(
            new CreateOfferCommand("Pan dulce", "Recién horneado", 50m, 20, 5, _now.AddHours(1), _now.AddDays(2), Category: null),
            CancellationToken.None);
        missingResult.IsFailure.Should().BeTrue();
        missingResult.Error.Code.Should().Be("Offer.CategoryRequired");

        var invalidResult = await strictHandler.Handle(
            new CreateOfferCommand("Pan dulce", "Recién horneado", 50m, 20, 5, _now.AddHours(1), _now.AddDays(2), Category: "Moda"),
            CancellationToken.None);
        invalidResult.IsFailure.Should().BeTrue();
        invalidResult.Error.Code.Should().Be("Offer.InvalidCategory");

        var validResult = await strictHandler.Handle(
            new CreateOfferCommand("Pan dulce", "Recién horneado", 50m, 20, 5, _now.AddHours(1), _now.AddDays(2), Category: "Bakery"),
            CancellationToken.None);
        validResult.IsSuccess.Should().BeTrue();
        validResult.Value.Category.Should().Be("Bakery");
    }
}
