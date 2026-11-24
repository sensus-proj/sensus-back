using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Implementación de mejor esfuerzo de <see cref="IOfferInteractionRecorder"/> (Spec 055, FR-055-08).
/// Captura fallos o expiraciones (>200 ms), emite métrica de error y nunca interrumpe la operación principal.
/// </summary>
public sealed class MongoOfferInteractionRecorder : IOfferInteractionRecorder
{
    private readonly IUserPseudonymizer _pseudonymizer;
    private readonly IOfferInteractionRepository _repository;
    private readonly IInteractionMetricsPublisher _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly InteractionSettings _settings;
    private readonly ILogger<MongoOfferInteractionRecorder> _logger;

    public MongoOfferInteractionRecorder(
        IUserPseudonymizer pseudonymizer,
        IOfferInteractionRepository repository,
        IInteractionMetricsPublisher metrics,
        IDateTimeProvider clock,
        IOptions<InteractionSettings> settings,
        ILogger<MongoOfferInteractionRecorder> logger)
    {
        _pseudonymizer = pseudonymizer;
        _repository = repository;
        _metrics = metrics;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RecordImpressionsAsync(
        Guid userId,
        string? snapshotId,
        string rankingVersion,
        int page,
        IReadOnlyList<OfferInteractionItem> items,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || userId == Guid.Empty || items.Count == 0)
        {
            return;
        }

        if (!ShouldSampleImpression(userId, snapshotId, page))
        {
            return;
        }

        await WriteBestEffortAsync(
            userId,
            OfferInteractionType.Impression,
            snapshotId,
            rankingVersion,
            page,
            items,
            cancellationToken);
    }

    public async Task RecordSingleAsync(
        Guid userId,
        OfferInteractionType type,
        OfferInteractionItem item,
        string rankingVersion = "direct",
        string? snapshotId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || userId == Guid.Empty)
        {
            return;
        }

        if (type == OfferInteractionType.Impression)
        {
            await RecordImpressionsAsync(userId, snapshotId, rankingVersion, page: 1, [item], cancellationToken);
            return;
        }

        await WriteBestEffortAsync(
            userId,
            type,
            snapshotId,
            rankingVersion,
            page: 1,
            [item],
            cancellationToken);
    }

    public async Task PurgeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        IReadOnlyList<PseudonymizedUser> pseudonyms = _pseudonymizer.ComputeAllKnownPseudonyms(userId);
        if (pseudonyms.Count == 0)
        {
            return;
        }

        await _repository.DeleteByPseudonymsAsync(
            pseudonyms.Select(p => (p.PseudoUserId, p.KeyVersion)).ToList(),
            cancellationToken);
    }

    private async Task WriteBestEffortAsync(
        Guid userId,
        OfferInteractionType type,
        string? snapshotId,
        string rankingVersion,
        int page,
        IReadOnlyList<OfferInteractionItem> items,
        CancellationToken cancellationToken)
    {
        try
        {
            PseudonymizedUser pseudonym = _pseudonymizer.Pseudonymize(userId);
            string resolvedRankingVersion = string.IsNullOrWhiteSpace(rankingVersion)
                ? (_settings.RankingVersion ?? "feed-v1")
                : rankingVersion.Trim();

            Result<OfferInteraction> interactionResult = OfferInteraction.Create(
                type,
                pseudonym.PseudoUserId,
                pseudonym.KeyVersion,
                snapshotId,
                resolvedRankingVersion,
                page,
                items,
                _clock.UtcNow);

            if (interactionResult.IsFailure)
            {
                _metrics.RecordWriteFailure(type);
                _logger.LogWarning(
                    "Interacción {InteractionType} descartada por validación ({ErrorCode}).",
                    type.ToWireName(),
                    interactionResult.Error.Code);
                return;
            }

            int timeoutMs = _settings.WriteTimeoutMs > 0 ? _settings.WriteTimeoutMs : 200;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            await _repository.InsertAsync(interactionResult.Value, timeoutCts.Token);
            _metrics.RecordWritten(type);
        }
        catch (Exception ex)
        {
            // FR-055-08 / NFR-055-02: fallo degradado con métrica, sin PII ni seudónimo en CloudWatch Logs.
            _metrics.RecordWriteFailure(type);
            _logger.LogWarning(
                ex,
                "Fallo al registrar interacción {InteractionType} (elementos: {ItemCount}); operación principal preservada.",
                type.ToWireName(),
                items.Count);
        }
    }

    private bool ShouldSampleImpression(Guid userId, string? snapshotId, int page)
    {
        double rate = _settings.ImpressionSampleRate;
        if (rate >= 1d)
        {
            return true;
        }

        if (rate <= 0d)
        {
            return false;
        }

        // Muestreo determinista por sesión/instantánea para evitar aleatoriedad no reproducible.
        string token = $"{userId:N}:{snapshotId ?? "nosnap"}:{page}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        uint bucket = BitConverter.ToUInt32(hash, 0);
        double normalized = bucket / (double)uint.MaxValue;
        return normalized < rate;
    }
}
