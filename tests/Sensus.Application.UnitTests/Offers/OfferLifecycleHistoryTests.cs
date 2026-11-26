using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.ChangeOfferStatus;
using Sensus.Application.Offers.Commands.ExpireOffers;
using Sensus.Application.Offers.Commands.RepublishOffer;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

public sealed class OfferLifecycleHistoryTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ChangeOfferStatusHandler_PersistsTransitionHistory()
    {
        var offerRepo = Substitute.For<IOfferRepository>();
        var claimRepo = Substitute.For<IOfferClaimRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var historyRepo = Substitute.For<IOfferStatusHistoryRepository>();

        Guid userId = Guid.NewGuid();
        Guid merchantId = Guid.NewGuid();
        Comercio comercio = Comercio.Reconstitute(merchantId, userId, "Café", "Dir", "06000", 19.43, -99.13, true, Now, MerchantCategory.Cafeterias);
        Offer offer = Offer.Create(merchantId, "Latte", "2x1 en café artesanal", 80m, 50, 10, Now.AddHours(-1), Now.AddDays(1), null, 19.43, -99.13, Now).Value;

        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        var handler = new ChangeOfferStatusCommandHandler(
            offerRepo,
            claimRepo,
            comercioRepo,
            currentUser,
            clock,
            NullLogger<ChangeOfferStatusCommandHandler>.Instance,
            statusHistoryRepository: historyRepo);

        var result = await handler.Handle(new ChangeOfferStatusCommand(offer.Id, "Paused"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await historyRepo.Received(1).AddAsync(
            Arg.Is<OfferStatusTransition>(t =>
                t.OfferId == offer.Id
                && t.FromStatus == OfferStatus.Published
                && t.ToStatus == OfferStatus.Paused
                && t.TransitionType == "Manual"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepublishOfferHandler_PersistsTransitionHistory()
    {
        var offerRepo = Substitute.For<IOfferRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var historyRepo = Substitute.For<IOfferStatusHistoryRepository>();

        Guid userId = Guid.NewGuid();
        Guid merchantId = Guid.NewGuid();
        Comercio comercio = Comercio.Reconstitute(merchantId, userId, "Café", "Dir", "06000", 19.43, -99.13, true, Now.AddDays(-5), MerchantCategory.Cafeterias);
        Offer offer = Offer.Create(merchantId, "Latte", "2x1 en café artesanal", 80m, 50, 10, Now.AddDays(-5), Now.AddDays(-1), null, 19.43, -99.13, Now.AddDays(-5)).Value;
        offer.Archive(Now.AddHours(-1));

        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Sensus.Domain.Common.Result.Success());
        var textModeration = Substitute.For<ITextModerationService>();
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", null, "Approved"));

        var handler = new RepublishOfferCommandHandler(
            offerRepo,
            comercioRepo,
            currentUser,
            clock,
            NullLogger<RepublishOfferCommandHandler>.Instance,
            auditRecorder,
            statusHistoryRepository: historyRepo,
            textModerationService: textModeration);

        var result = await handler.Handle(new RepublishOfferCommand(offer.Id, Now, Now.AddDays(2), 15), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await historyRepo.Received(1).AddAsync(
            Arg.Is<OfferStatusTransition>(t =>
                t.OfferId == offer.Id
                && t.FromStatus == OfferStatus.Archived
                && t.ToStatus == OfferStatus.Published
                && t.TransitionType == "Republish"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpireOffersHandler_PersistsBatchTransition_WhenOffersExpire()
    {
        var offerRepo = Substitute.For<IOfferRepository>();
        var clock = Substitute.For<IDateTimeProvider>();
        var historyRepo = Substitute.For<IOfferStatusHistoryRepository>();

        clock.UtcNow.Returns(Now);
        offerRepo.ExpireOffersAsync(Now, Arg.Any<CancellationToken>()).Returns(4);

        var handler = new ExpireOffersCommandHandler(
            offerRepo,
            clock,
            NullLogger<ExpireOffersCommandHandler>.Instance,
            statusHistoryRepository: historyRepo);

        var result = await handler.Handle(new ExpireOffersCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await historyRepo.Received(1).AddAsync(
            Arg.Is<OfferStatusTransition>(t =>
                t.ToStatus == OfferStatus.Expired
                && t.TransitionType == "AutomaticExpiration:4"),
            Arg.Any<CancellationToken>());
    }
}
