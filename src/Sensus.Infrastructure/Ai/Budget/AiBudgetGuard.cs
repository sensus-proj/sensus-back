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
/// y cuota diaria (20 mensajes/usuario) mediante operaciones atómicas en Redis (INCRBYFLOAT/INCR).
/// Si Redis no está disponible, opera en modo fail-closed para proteger el presupuesto.
/// </summary>
public sealed class AiBudgetGuard : IAiBudgetGuard
{
    private readonly ICacheService _cacheService;
    private readonly SensiSettings _settings;
    private readonly ILogger<AiBudgetGuard> _logger;

    public AiBudgetGuard(
        ICacheService cacheService,
        IOptions<OpenAiSettings> settings,
        ILogger<AiBudgetGuard> logger)
    {
        _cacheService = cacheService;
        _settings = settings.Value.Sensi;
        _logger = logger;
    }

    public async Task<bool> CanExecuteTurnAsync(
        Guid userId,
        decimal estimatedCostUsd,
        CancellationToken cancellationToken = default)
    {
        // Fail-closed si Redis no está disponible para evitar arranques en frío sin techo en Lambda
        if (!_cacheService.IsAvailable)
        {
            _logger.LogWarning("Redis no está disponible. Operando en modo fail-closed para proteger el presupuesto EX-001.");
            return false;
        }

        // 1. Validar cuota diaria del usuario
        var todayCount = await GetTodayMessageCountAsync(userId, cancellationToken);
        if (todayCount >= _settings.DailyMessageLimitPerUser)
        {
            _logger.LogWarning(
                "Usuario {UserId} alcanzó el límite diario de {Limit} mensajes de Sensi IA.",
                userId,
                _settings.DailyMessageLimitPerUser);
            return false;
        }

        // 2. Validar techo mensual estricto (EX-001)
        var totalCap = (decimal)_settings.TotalBudgetCapUsd;
        var currentSpend = await GetMonthlySpendAsync(cancellationToken);
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
        if (!_cacheService.IsAvailable)
        {
            return false;
        }

        var reasoningKey = GetMonthlyReasoningSpendKey();
        var currentReasoningSpend = await _cacheService.GetAsync<double?>(reasoningKey, cancellationToken) ?? 0.0;
        return (decimal)currentReasoningSpend < (decimal)_settings.ReasoningBudgetCapUsd;
    }

    public async Task RecordSpendAsync(
        Guid userId,
        decimal actualCostUsd,
        AiRoutingTier routingTier = AiRoutingTier.Fast,
        CancellationToken cancellationToken = default)
    {
        if (!_cacheService.IsAvailable)
        {
            _logger.LogWarning("Redis no está disponible al registrar gasto de {Cost:F6} USD para usuario {UserId}.", actualCostUsd, userId);
            return;
        }

        var spendKey = GetMonthlySpendKey();
        var dailyKey = GetDailyCountKey(userId);

        // Incremento atómico en Redis (INCRBYFLOAT)
        var newTotal = await _cacheService.IncrementByFloatAsync(
            spendKey,
            (double)actualCostUsd,
            TimeSpan.FromDays(45),
            cancellationToken);

        // Sub-techo del nivel reasoning (sensus:sensi:spend:reasoning:yyyyMM)
        if (routingTier == AiRoutingTier.Reasoning)
        {
            var reasoningKey = GetMonthlyReasoningSpendKey();
            await _cacheService.IncrementByFloatAsync(
                reasoningKey,
                (double)actualCostUsd,
                TimeSpan.FromDays(45),
                cancellationToken);
        }

        // Contador diario de mensajes por usuario (INCR)
        var newDaily = await _cacheService.IncrementAsync(
            dailyKey,
            1,
            TimeSpan.FromDays(2),
            cancellationToken);

        _logger.LogInformation(
            "Gasto registrado Sensi IA: +{Cost:F6} USD ({Tier}). Acumulado mes: {TotalSpend:F4} USD. Mensajes hoy usuario {UserId}: {DailyCount}",
            actualCostUsd,
            routingTier,
            newTotal ?? 0.0,
            userId,
            newDaily ?? 0);
    }

    public async Task<int> GetTodayMessageCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_cacheService.IsAvailable)
        {
            return 0;
        }

        var key = GetDailyCountKey(userId);
        var cached = await _cacheService.GetAsync<long?>(key, cancellationToken);
        return cached.HasValue ? (int)cached.Value : 0;
    }

    private async Task<decimal> GetMonthlySpendAsync(CancellationToken cancellationToken)
    {
        if (!_cacheService.IsAvailable)
        {
            return 0m;
        }

        var key = GetMonthlySpendKey();
        var cached = await _cacheService.GetAsync<double?>(key, cancellationToken);
        return cached.HasValue ? (decimal)cached.Value : 0m;
    }

    private static string GetMonthlySpendKey() =>
        $"sensus:sensi:spend:{DateTime.UtcNow:yyyyMM}";

    private static string GetMonthlyReasoningSpendKey() =>
        $"sensus:sensi:spend:reasoning:{DateTime.UtcNow:yyyyMM}";

    private static string GetDailyCountKey(Guid userId) =>
        $"sensus:sensi:user:{userId}:daily:{DateTime.UtcNow:yyyyMMdd}";
}
