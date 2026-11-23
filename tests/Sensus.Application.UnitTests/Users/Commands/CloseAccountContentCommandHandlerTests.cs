using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Application.Users.Commands.CloseAccountContent;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Users.Commands;

/// <summary>Cierre comercial y anonimización de contenido (Spec 061, CA-061-01 a CA-061-06).</summary>
public class CloseAccountContentCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly IFeedPostRepository _posts = Substitute.For<IFeedPostRepository>();
    private readonly IFeedCommentRepository _comments = Substitute.For<IFeedCommentRepository>();
    private readonly IOfferClaimRepository _claims = Substitute.For<IOfferClaimRepository>();
    private readonly IComercioRepository _comercios = Substitute.For<IComercioRepository>();
    private readonly IOfferRepository _offers = Substitute.For<IOfferRepository>();
    private readonly ISanctionRepository _sanctions = Substitute.For<ISanctionRepository>();
    private readonly IModerationAuditRepository _audit = Substitute.For<IModerationAuditRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private CloseAccountContentCommandHandler CreateHandler() =>
        new(
            _posts,
            _comments,
            _claims,
            _comercios,
            _offers,
            _sanctions,
            _audit,
            _clock,
            Options.Create(new AccountDeletionSettings { ModerationAuditRetentionDays = 90 }),
            cacheService: _cache);

    private static Offer BuildOffer(Guid merchantId) =>
        Offer.Create(merchantId, "Oferta", "Descripción", 100m, 10, 2,
            Now.AddHours(-1), Now.AddDays(1), null, 20.05, -99.34, Now).Value;

    [Fact]
    public async Task Handle_ShouldAnonymizeExpireClaimsBeforeArchivingAndCopyAudit()
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Tienda", "Calle 1", "42800", 20.05, -99.34).Value;
        Offer offer = BuildOffer(comercio.Id);
        var evidence = new SanctionEvidence(null, "Rejected", ["spam"],
            new Dictionary<string, double> { ["spam"] = 0.9 }, "0.8", "Quarantine");
        Sanction sanction = Sanction.CreateAutomatic(userId, "Spam", evidence, Now, null).Value;

        _clock.UtcNow.Returns(Now);
        _comercios.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        _posts.AnonymizeByAuthorAsync(userId, Now, Arg.Any<CancellationToken>()).Returns(3);
        _comments.AnonymizeByAuthorAsync(userId, Arg.Any<CancellationToken>()).Returns(5);

        int order = 0;
        int claimsExpiredOrder = -1;
        int offerArchivedOrder = -1;
        _claims.ExpireActiveByMerchantAsync(comercio.Id, Now, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                claimsExpiredOrder = ++order;
                return 2;
            });
        _offers.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>()).Returns(new[] { offer });
        _offers.UpdateAsync(offer, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                offerArchivedOrder = ++order;
                return Task.CompletedTask;
            });
        _sanctions.GetAllByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { sanction });
        _audit.AddIfNotExistsAsync(Arg.Any<ModerationAudit>(), Arg.Any<CancellationToken>()).Returns(true);

        ModerationAudit? captured = null;
        _audit.AddIfNotExistsAsync(Arg.Do<ModerationAudit>(a => captured = a), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<CloseAccountContentResult> result =
            await CreateHandler().Handle(new CloseAccountContentCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PostsAnonymized.Should().Be(3);
        result.Value.CommentsAnonymized.Should().Be(5);
        result.Value.ClaimsExpired.Should().Be(2);
        result.Value.OffersArchived.Should().Be(1);
        result.Value.AuditEntries.Should().Be(1);
        claimsExpiredOrder.Should().BeLessThan(offerArchivedOrder);
        offer.Status.Should().Be(OfferStatus.Archived);
        await _cache.Received(1).InvalidateAsync(offer.Id, Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.SubjectKey.Should().Be(ModerationAudit.ComputeSubjectKey(userId));
        captured.ExpiresAtUtc.Should().Be(Now.AddDays(90));
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenAuditEntryAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var evidence = new SanctionEvidence(null, "Rejected", [], new Dictionary<string, double>(), "0.8", "Reject");
        Sanction sanction = Sanction.CreateAutomatic(userId, "Spam", evidence, Now, null).Value;

        _clock.UtcNow.Returns(Now);
        _comercios.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns((Comercio?)null);
        _sanctions.GetAllByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { sanction });
        _audit.AddIfNotExistsAsync(Arg.Any<ModerationAudit>(), Arg.Any<CancellationToken>()).Returns(false);

        Result<CloseAccountContentResult> result =
            await CreateHandler().Handle(new CloseAccountContentCommand(userId), CancellationToken.None);

        result.Value.AuditEntries.Should().Be(0);
        await _claims.DidNotReceiveWithAnyArgs().ExpireActiveByMerchantAsync(default, default, default);
        await _offers.DidNotReceiveWithAnyArgs().GetByMerchantIdAsync(default, default);
    }
}
