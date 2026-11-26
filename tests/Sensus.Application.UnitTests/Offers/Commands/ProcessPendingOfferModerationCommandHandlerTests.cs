using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Application.Offers.Commands.ProcessPendingOfferModeration;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public sealed class ProcessPendingOfferModerationCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IModerationAuditRecorder _auditRecorder = Substitute.For<IModerationAuditRecorder>();
    private readonly ITextModerationService _textModeration = Substitute.For<ITextModerationService>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly INearbyOfferNotifier _notifier = Substitute.For<INearbyOfferNotifier>();

    private static (Offer Offer, Comercio Comercio) CreatePending(int retryCount = 0)
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Tienda Prueba", "Calle 1", "03100", 19.43, -99.13).Value;
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            comercio.Id,
            "Oferta pendiente",
            "Descripción válida para moderar",
            100m,
            20,
            80m,
            5,
            Now.AddHours(-1),
            Now.AddDays(2),
            OfferStatus.PendingModeration,
            null,
            19.43,
            -99.13,
            Now.AddHours(-1),
            moderationRetryCount: retryCount,
            nextModerationRetryAtUtc: Now.AddSeconds(-10));
        return (offer, comercio);
    }

    private ProcessPendingOfferModerationCommandHandler BuildHandler(int maxAttempts = 5)
    {
        _clock.UtcNow.Returns(Now);
        _auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return new ProcessPendingOfferModerationCommandHandler(
            _offerRepository,
            _comercioRepository,
            _clock,
            _auditRecorder,
            NullLogger<ProcessPendingOfferModerationCommandHandler>.Instance,
            _textModeration,
            cacheService: _cache,
            nearbyOfferNotifier: _notifier,
            retrySettings: Options.Create(new OfferModerationRetrySettings
            {
                MaxAttempts = maxAttempts,
                RetryDelaySeconds = 60,
                BatchSize = 20
            }));
    }

    [Fact]
    public async Task Handle_ShouldPublishAuditInvalidateCacheAndNotify_WhenModerationRecoversAndApproves()
    {
        var (offer, comercio) = CreatePending();
        _offerRepository.GetPendingModerationDueAsync(Now, 20, Arg.Any<CancellationToken>()).Returns([offer]);
        _comercioRepository.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>()).Returns(comercio);
        _textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", null, "Approved"));

        var result = await BuildHandler().Handle(new ProcessPendingOfferModerationCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CheckedCount.Should().Be(1);
        result.Value.PublishedCount.Should().Be(1);
        offer.Status.Should().Be(OfferStatus.Published);
        offer.NextModerationRetryAtUtc.Should().BeNull();
        await _auditRecorder.Received(1).RecordAsync(
            Arg.Is<ModerationAuditEntry>(e => e.TargetId == offer.Id && e.Verdict == ModerationVerdict.Approved),
            Arg.Any<CancellationToken>());
        await _offerRepository.Received(1).UpdateAsync(offer, Arg.Any<CancellationToken>());
        await _cache.Received(1).InvalidateAsync(offer.Id, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAsync(offer, comercio.UsuarioId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRejectWithoutNotifying_WhenModerationRecoversAndRejects()
    {
        var (offer, _) = CreatePending();
        _offerRepository.GetPendingModerationDueAsync(Now, 20, Arg.Any<CancellationToken>()).Returns([offer]);
        _textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "Contenido prohibido", ["violence"], "Rejected"));

        var result = await BuildHandler().Handle(new ProcessPendingOfferModerationCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCount.Should().Be(1);
        result.Value.PublishedCount.Should().Be(0);
        offer.Status.Should().Be(OfferStatus.Rejected);
        await _auditRecorder.Received(1).RecordBestEffortAsync(
            Arg.Is<ModerationAuditEntry>(e => e.TargetId == offer.Id && e.Verdict == ModerationVerdict.Rejected),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_ShouldRescheduleUntilMaxAttempts_ThenRejectFailClosed()
    {
        var (offer, _) = CreatePending(retryCount: 1);
        _offerRepository.GetPendingModerationDueAsync(Now, 20, Arg.Any<CancellationToken>()).Returns([offer]);
        _textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TextModerationResult>(new TimeoutException("OpenAI timeout")));

        var handler = BuildHandler(maxAttempts: 3);

        var retry2 = await handler.Handle(new ProcessPendingOfferModerationCommand(), CancellationToken.None);
        retry2.Value.RescheduledCount.Should().Be(1);
        offer.Status.Should().Be(OfferStatus.PendingModeration);
        offer.ModerationRetryCount.Should().Be(2);
        offer.NextModerationRetryAtUtc.Should().Be(Now.AddSeconds(60));

        var retry3 = await handler.Handle(new ProcessPendingOfferModerationCommand(), CancellationToken.None);
        retry3.Value.RejectedCount.Should().Be(1);
        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.ModerationRetryCount.Should().Be(3);
        offer.NextModerationRetryAtUtc.Should().BeNull();
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_ShouldKeepPendingAndNotPublish_WhenAuditPersistenceFails()
    {
        var (offer, _) = CreatePending();
        _offerRepository.GetPendingModerationDueAsync(Now, 20, Arg.Any<CancellationToken>()).Returns([offer]);
        _textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", null, "Approved"));

        var handler = BuildHandler();
        _auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Unavailable("ModerationAudit.Unavailable", "Sin auditoría")));

        var result = await handler.Handle(new ProcessPendingOfferModerationCommand(), CancellationToken.None);

        result.Value.PublishedCount.Should().Be(0);
        result.Value.RescheduledCount.Should().Be(1);
        offer.Status.Should().Be(OfferStatus.PendingModeration);
        await _offerRepository.DidNotReceive().UpdateAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default, default);
    }
}
