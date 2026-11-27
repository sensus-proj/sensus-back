using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Queries.GetMerchantOffers;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Queries;

public class GetMerchantOffersQueryHandlerTests
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOfferLikeRepository _offerLikeRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly GetMerchantOffersQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] ExpectedTitles = { "Oferta 1", "Oferta 2" };

    public GetMerchantOffersQueryHandlerTests()
    {
        _offerRepository = Substitute.For<IOfferRepository>();
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _offerLikeRepository = Substitute.For<IOfferLikeRepository>();
        _offerClaimRepository = Substitute.For<IOfferClaimRepository>();

        _currentUserService.UserId.Returns(_userId.ToString());
        _offerLikeRepository.CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int>());
        _offerLikeRepository.GetLikedOfferIdsAsync(
                Arg.Any<Guid>(),
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
        _offerClaimRepository.CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OfferClaimCounts>());

        _handler = new GetMerchantOffersQueryHandler(
            _offerRepository,
            _comercioRepository,
            _currentUserService,
            _offerLikeRepository,
            _offerClaimRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsNull()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIdIsNotValidGuid()
    {
        _currentUserService.UserId.Returns("not-a-guid");

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNoMerchantExists()
    {
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(null));

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NoActiveMerchant");
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedOffers_WhenMerchantStoreIsClosed()
    {
        var comercio = Comercio.Create(_userId, "Comercio Cerrado", "Calle 123", "06000", 19.4, -99.1).Value;
        comercio.Deactivate();
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));
        List<Offer> offers = [BuildOffer(comercio.Id, "Oferta Cerrada", _now.AddHours(-1))];
        SetupPagedOffers(comercio.Id, null, 1, 20, offers, 1);

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(o => o.Title == "Oferta Cerrada");
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedOffers_WhenNoStatusFilterSpecified()
    {
        Comercio comercio = SetupActiveMerchant();
        List<Offer> offers =
        [
            BuildOffer(comercio.Id, "Oferta 1", _now.AddHours(-1)),
            BuildOffer(comercio.Id, "Oferta 2", _now.AddHours(-2))
        ];
        SetupPagedOffers(comercio.Id, null, 1, 20, offers, offers.Count);

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(o => o.Title).Should().Contain(ExpectedTitles);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ShouldFilterArchivedOffers_InRepository()
    {
        Comercio comercio = SetupActiveMerchant();
        Offer archived = BuildOffer(comercio.Id, "Oferta Archivada", _now, OfferStatus.Archived);
        SetupPagedOffers(comercio.Id, OfferStatus.Archived, 2, 10, [archived], 11);

        var result = await _handler.Handle(
            new GetMerchantOffersQuery(StatusFilter: "Archived", Page: 2, PageSize: 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Status.Should().Be("Archived");
        result.Value.TotalCount.Should().Be(11);
        result.Value.PageNumber.Should().Be(2);
        await _offerRepository.Received(1).GetByMerchantIdPagedAsync(
            comercio.Id,
            OfferStatus.Archived,
            2,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNoOffersExist()
    {
        Comercio comercio = SetupActiveMerchant();
        SetupPagedOffers(comercio.Id, null, 1, 20, [], 0);

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldEnrichMetrics_WithViewsClaimsAndLikes()
    {
        Comercio comercio = SetupActiveMerchant();
        var offerWithViews = Offer.Reconstitute(
            Guid.NewGuid(), comercio.Id, "Oferta con métricas", "Desc", 100m, 10, 90m, 5,
            _now.AddHours(1), _now.AddDays(1), OfferStatus.Published, null, null, null,
            _now, viewCount: 7);
        SetupPagedOffers(comercio.Id, null, 1, 20, [offerWithViews], 1);

        _offerClaimRepository.CountByOfferIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(offerWithViews.Id)),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OfferClaimCounts>
            {
                [offerWithViews.Id] = new OfferClaimCounts(ActiveClaims: 3, Redemptions: 4)
            });
        _offerLikeRepository.CountByOfferIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(offerWithViews.Id)),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [offerWithViews.Id] = 9 });
        _offerLikeRepository.GetLikedOfferIdsAsync(
                _userId,
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { offerWithViews.Id });

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ViewsCount.Should().Be(7);
        item.ActiveClaimsCount.Should().Be(3);
        item.RedemptionsCount.Should().Be(4);
        item.LikesCount.Should().Be(9);
        item.IsLikedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldQueryOnlyTheAuthenticatedMerchantStore()
    {
        Comercio ownStore = SetupActiveMerchant();
        Comercio otherStore = Comercio.Create(Guid.NewGuid(), "Otro comercio", "Calle 999", "06000", 19.5, -99.2).Value;
        SetupPagedOffers(ownStore.Id, null, 1, 20, [BuildOffer(ownStore.Id, "Propia", _now)], 1);
        SetupPagedOffers(otherStore.Id, null, 1, 20, [BuildOffer(otherStore.Id, "Ajena", _now)], 1);

        var result = await _handler.Handle(new GetMerchantOffersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.MerchantId.Should().Be(ownStore.Id);
        await _offerRepository.Received(1).GetByMerchantIdPagedAsync(
            ownStore.Id, null, 1, 20, Arg.Any<CancellationToken>());
        await _offerRepository.DidNotReceive().GetByMerchantIdPagedAsync(
            otherStore.Id,
            Arg.Any<OfferStatus?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenStatusIsInvalid()
    {
        SetupActiveMerchant();

        var result = await _handler.Handle(
            new GetMerchantOffersQuery(StatusFilter: "NoExiste"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Sensus.Domain.Common.ErrorType.Validation);
        await _offerRepository.DidNotReceiveWithAnyArgs().GetByMerchantIdPagedAsync(
            default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_ShouldNormalizePageSize_WhenCalledDirectly()
    {
        Comercio comercio = SetupActiveMerchant();
        SetupPagedOffers(comercio.Id, null, 1, 100, [], 0);

        var result = await _handler.Handle(
            new GetMerchantOffersQuery(Page: 0, PageSize: 200),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(100);
        await _offerRepository.Received(1).GetByMerchantIdPagedAsync(
            comercio.Id, null, 1, 100, Arg.Any<CancellationToken>());
    }

    private Comercio SetupActiveMerchant()
    {
        var comercio = Comercio.Create(_userId, "Comercio Activo", "Calle 123", "06000", 19.4, -99.1).Value;
        _comercioRepository.GetByUsuarioIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));
        return comercio;
    }

    private void SetupPagedOffers(
        Guid merchantId,
        OfferStatus? status,
        int page,
        int pageSize,
        IReadOnlyList<Offer> offers,
        int totalCount)
    {
        _offerRepository.GetByMerchantIdPagedAsync(merchantId, status, page, pageSize, Arg.Any<CancellationToken>())
            .Returns((offers, totalCount));
    }

    private Offer BuildOffer(Guid merchantId, string title, DateTime createdAt, OfferStatus status = OfferStatus.Published) =>
        Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            title,
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            status,
            null,
            null,
            null,
            createdAt);
}
