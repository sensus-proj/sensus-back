using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Services;

namespace Sensus.Infrastructure.Ai.Budget;

/// <summary>
/// Guardián de presupuesto mensual y cuota diaria por usuario para Sensi IA (FR-046-13, NFR-046-02, EX-001).
/// Aplica límite mensual total (5.00 USD), sub-techo reasoning (3.00 USD), alerta al 80% (4.00 USD)
/// y cuota diaria (20 mensajes/usuario) mediante contadores atómicos en <see cref="IBudgetCounterStore"/>
/// (Amazon DynamoDB, Spec 049). Si el almacén no está disponible, falla o expira, opera en modo
/// fail-closed para proteger el presupuesto.
/// </summary>
public sealed class AiBudgetGuard : IAiBudgetGuard
{
    private static readonly TimeSpan MonthlyCounterTtl = TimeSpan.FromDays(45);
    private static readonly TimeSpan DailyCounterTtl = TimeSpan.FromDays(2);

    private readonly IBudgetCounterStore _store;
    private readonly SensiSettings _settings;
    private readonly ILogger<AiBudgetGuard> _logger;

    public AiBudgetGuard(
        IBudgetCounterStore store,
        IOptions<OpenAiSettings> settings,
        ILogger<AiBudgetGuard> logger)
    {
        _store = store;
        _settings = settings.Value.Sensi;
        _logger = logger;
    }

    public async Task<bool> CanExecuteTurnAsync(
        Guid userId,
        decimal estimatedCostUsd,
        CancellationToken cancellationToken = default)
    {
        // Fail-closed si el almacén no está disponible para evitar arranques en frío sin techo en Lambda
        if (!_store.IsAvailable)
        {
            _logger.LogWarning("El almacén de contadores no está disponible. Operando en modo fail-closed para proteger el presupuesto EX-001.");
            return false;
        }

        // 1. Validar cuota diaria del usuario
        var todayCount = await _store.GetAsync(GetDailyCountKey(userId), cancellationToken);
        if (todayCount is null)
        {
            _logger.LogWarning("No fue posible leer la cuota diaria de Sensi IA. Operando en modo fail-closed (EX-001).");
            return false;
        }

        if (todayCount.Value >= _settings.DailyMessageLimitPerUser)
        {
            _logger.LogWarning(
                "Usuario {UserId} alcanzó el límite diario de {Limit} mensajes de Sensi IA.",
                userId,
                _settings.DailyMessageLimitPerUser);
            return false;
        }

        // 2. Validar techo mensual estricto (EX-001)
        var spend = await _store.GetAsync(GetMonthlySpendKey(), cancellationToken);
        if (spend is null)
        {
            _logger.LogWarning("No fue posible leer el gasto mensual de Sensi IA. Operando en modo fail-closed (EX-001).");
            return false;
        }

        var currentSpend = (decimal)spend.Value;
        var totalCap = (decimal)_settings.TotalBudgetCapUsd;
        if (currentSpend + estimatedCostUsd > totalCap)
        {
            _logger.LogWarning(
                "Techo de presupuesto mensual de Sensi IA alcanzado: {CurrentSpend:F4} + {EstCost:F4} > {Cap:F2} USD.",
                currentSpend,
                estimatedCostUsd,
                totalCap);
            return false;
        }

        // 3. Alarma al 80%
        var alertThreshold = totalCap * (decimal)_settings.AlertThresholdRatio;
        if (currentSpend >= alertThreshold)
        {
            _logger.LogWarning(
                "ALERTA EX-001: Consumo mensual de Sensi IA superó el umbral del {Ratio:P0} ({CurrentSpend:F4} / {Cap:F2} USD).",
                _settings.AlertThresholdRatio,
                currentSpend,
                _settings.TotalBudgetCapUsd);
        }

        return true;
    }

    public async Task<bool> IsReasoningBudgetAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_store.IsAvailable)
        {
            return false;
        }

        var reasoningSpend = await _store.GetAsync(GetMonthlyReasoningSpendKey(), cancellationToken);
        return reasoningSpend.HasValue
            && (decimal)reasoningSpend.Value < (decimal)_settings.ReasoningBudgetCapUsd;
    }

    public async Task RecordSpendAsync(
        Guid userId,
        decimal actualCostUsd,
        AiRoutingTier routingTier = AiRoutingTier.Fast,
        CancellationToken cancellationToken = default)
    {
        if (!_store.IsAvailable)
        {
            _logger.LogWarning("El almacén de contadores no está disponible al registrar gasto de {Cost:F6} USD.", actualCostUsd);
            return;
        }

        // Incremento atómico del gasto mensual total
        var newTotal = await _store.IncrementAsync(
            GetMonthlySpendKey(),
            (double)actualCostUsd,
            MonthlyCounterTtl,
            cancellationToken);

        // Sub-techo del nivel reasoning
        if (routingTier == AiRoutingTier.Reasoning)
        {
            await _store.IncrementAsync(
                GetMonthlyReasoningSpendKey(),
                (double)actualCostUsd,
                MonthlyCounterTtl,
                cancellationToken);
        }

        // Contador diario de mensajes por usuario
        var newDaily = await _store.IncrementAsync(
            GetDailyCountKey(userId),
            1d,
            DailyCounterTtl,
            cancellationToken);

        if (newTotal is null || newDaily is null)
        {
            _logger.LogWarning("No se pudo registrar por completo el gasto de Sensi IA ({Cost:F6} USD, {Tier}).", actualCostUsd, routingTier);
            return;
        }

        _logger.LogInformation(
            "Gasto registrado Sensi IA: +{Cost:F6} USD ({Tier}). Acumulado mes: {TotalSpend:F4} USD. Mensajes hoy usuario {UserId}: {DailyCount}",
            actualCostUsd,
            routingTier,
            newTotal.Value,
            userId,
            (long)newDaily.Value);
    }

    public async Task<int> GetTodayMessageCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_store.IsAvailable)
        {
            return 0;
        }

        var count = await _store.GetAsync(GetDailyCountKey(userId), cancellationToken);
        return count.HasValue ? (int)count.Value : 0;
    }

    private static string GetMonthlySpendKey() =>
        $"spend:{DateTime.UtcNow:yyyyMM}";

    private static string GetMonthlyReasoningSpendKey() =>
        $"spend:reasoning:{DateTime.UtcNow:yyyyMM}";

    private static string GetDailyCountKey(Guid userId) =>
        $"daily:{userId}:{DateTime.UtcNow:yyyyMMdd}";
}
