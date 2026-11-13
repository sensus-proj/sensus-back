using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Queries.GetMerchantOffers;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Queries;

public class GetMerchantOffersQueryHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetMerchantOffersQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] ExpectedTitles = { "Oferta 1", "Oferta 2" };

    public GetMerchantOffersQueryHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _currentUserService.UserId.Returns(_userId.ToString());

        _handler = new GetMerchantOffersQueryHandler(
            _offerRepository,
            _comercioRepository,
            _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsNull()
    {
        _currentUserService.UserId.Returns((string?)null);

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsNotValidGuid()
    {
        _currentUserService.UserId.Returns("not-a-guid");

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNoMerchantExists()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenMerchantIsNotActive()
    {
        var comercio = Comercio.Create(_userId, "Comercio Inactivo", "Calle 123", "06000", 19.4, -99.1).Value;
        comercio.Deactivate();

        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldReturnAllOffers_WhenNoStatusFilterSpecified()
    {
        var comercio = Comercio.Create(_userId, "Comercio Activo", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var offer1 = Offer.Create(comercio.Id, "Oferta 1", "Desc 1", 100m, 10, 5, _now.AddHours(1), _now.AddDays(2), nowUtc: _now).Value;
        var offer2 = Offer.Create(comercio.Id, "Oferta 2", "Desc 2", 200m, 20, 10, _now.AddHours(1), _now.AddDays(3), nowUtc: _now).Value;

        var offersList = (IReadOnlyList<Offer>)new List<Offer> { offer1, offer2 };
        _offerRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(offersList));

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(o => o.Title).Should().Contain(ExpectedTitles);
    }

    [Fact]
    public async Task Handle_ShouldReturnFilteredOffers_WhenStatusFilterApplied()
    {
        var comercio = Comercio.Create(_userId, "Comercio Activo", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var publishedOffer = Offer.Create(comercio.Id, "Oferta Publicada", "Desc 1", 100m, 10, 5, _now.AddHours(1), _now.AddDays(2), nowUtc: _now).Value;
        var expiredOffer = Offer.Reconstitute(
            Guid.NewGuid(), comercio.Id, "Oferta Expirada", "Desc 2", 200m, 20, 160m, 0,
            _now.AddDays(-5), _now.AddDays(-1), OfferStatus.Expired, null, null, null, _now.AddDays(-5));

        var offersList = (IReadOnlyList<Offer>)new List<Offer> { publishedOffer, expiredOffer };
        _offerRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(offersList));

        var query = new GetMerchantOffersQuery(StatusFilter: "Published");
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Be("Oferta Publicada");
        result.Value[0].Status.Should().Be("Published");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoOffersExist()
    {
        var comercio = Comercio.Create(_userId, "Comercio Activo", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var emptyList = (IReadOnlyList<Offer>)new List<Offer>();
        _offerRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(emptyList));

        var query = new GetMerchantOffersQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
