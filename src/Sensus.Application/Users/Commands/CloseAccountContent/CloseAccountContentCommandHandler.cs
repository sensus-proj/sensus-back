using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.CloseAccountContent;

/// <summary>
/// Anonimiza publicaciones y comentarios, expira los apartados activos del comercio antes de archivar
/// sus ofertas y copia la evidencia de moderación a una auditoría anonimizada con TTL (Spec 061).
/// </summary>
public sealed class CloseAccountContentCommandHandler
    : IRequestHandler<CloseAccountContentCommand, Result<CloseAccountContentResult>>
{
    private readonly IFeedPostRepository _posts;
    private readonly IFeedCommentRepository _comments;
    private readonly IOfferClaimRepository _claims;
    private readonly IComercioRepository _comercios;
    private readonly IOfferRepository _offers;
    private readonly ISanctionRepository _sanctions;
    private readonly IModerationAuditRepository _moderationAudit;
    private readonly IDateTimeProvider _clock;
    private readonly TimeSpan _auditRetention;
    private readonly ICacheService? _cacheService;
    private readonly ILogger<CloseAccountContentCommandHandler>? _logger;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public CloseAccountContentCommandHandler(
        IFeedPostRepository posts,
        IFeedCommentRepository comments,
        IOfferClaimRepository claims,
        IComercioRepository comercios,
        IOfferRepository offers,
        ISanctionRepository sanctions,
        IModerationAuditRepository moderationAudit,
        IDateTimeProvider clock,
        IOptions<AccountDeletionSettings> settings,
        ICacheService? cacheService = null,
        ILogger<CloseAccountContentCommandHandler>? logger = null,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _posts = posts;
        _comments = comments;
        _claims = claims;
        _comercios = comercios;
        _offers = offers;
        _sanctions = sanctions;
        _moderationAudit = moderationAudit;
        _clock = clock;
        _auditRetention = TimeSpan.FromDays(Math.Max(1, settings.Value.ModerationAuditRetentionDays));
        _cacheService = cacheService;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result<CloseAccountContentResult>> Handle(
        CloseAccountContentCommand request,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);

        int postsAnonymized = await _posts.AnonymizeByAuthorAsync(request.UserId, now, cancellationToken);
        int commentsAnonymized = await _comments.AnonymizeByAuthorAsync(request.UserId, cancellationToken);

        int claimsExpired = 0;
        int offersArchived = 0;
        Comercio? comercio = await _comercios.GetByUsuarioIdAsync(request.UserId, cancellationToken);
        if (comercio is not null)
        {
            // FR-061-03: los apartados se expiran ANTES de archivar las ofertas.
            claimsExpired = await _claims.ExpireActiveByMerchantAsync(comercio.Id, now, cancellationToken);

            IReadOnlyList<Offer> offers = await _offers.GetByMerchantIdAsync(comercio.Id, cancellationToken);
            foreach (Offer offer in offers.Where(o => o.Status != OfferStatus.Archived))
            {
                offer.Archive(now);
                await _offers.UpdateAsync(offer, cancellationToken);
                if (_cacheService is not null)
                {
                    await _cacheService.InvalidateAsync(offer.Id, cancellationToken);
                }

                offersArchived++;
            }
        }

        IReadOnlyList<Sanction> sanctions = await _sanctions.GetAllByUserAsync(request.UserId, cancellationToken);
        string subjectKey = ModerationAudit.ComputeSubjectKey(request.UserId);
        int auditEntries = 0;
        foreach (Sanction sanction in sanctions)
        {
            ModerationAudit? entry = ModerationAudit.FromSanction(sanction, subjectKey, _auditRetention, now);
            if (entry is not null && await _moderationAudit.AddIfNotExistsAsync(entry, cancellationToken))
            {
                auditEntries++;
            }
        }

        _logger?.LogInformation(
            "SecurityAudit AccountContentAnonymized Posts={Posts} Comments={Comments} ClaimsExpired={Claims} OffersArchived={Offers} AuditEntries={Audit} CorrelationId={CorrelationId}",
            postsAnonymized,
            commentsAnonymized,
            claimsExpired,
            offersArchived,
            auditEntries,
            _correlationIdAccessor?.CorrelationId ?? "unknown");

        return Result.Success(new CloseAccountContentResult(
            postsAnonymized,
            commentsAnonymized,
            claimsExpired,
            offersArchived,
            auditEntries));
    }
}
