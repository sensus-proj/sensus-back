using System.ComponentModel.DataAnnotations;

namespace Sensus.Application.Common.Settings;

public sealed class TextCategoryThreshold
{
    public double Low { get; set; } = 0.35;
    public double High { get; set; } = 0.75;
}

public sealed class SensiSettings
{
    public const string SectionName = "OpenAiSettings:Sensi";

    [Required(ErrorMessage = "El modelo de moderación de Sensi es obligatorio.")]
    public string ModerationModel { get; set; } = "omni-moderation-latest";

    [Required(ErrorMessage = "El modelo rápido de Sensi es obligatorio.")]
    public string FastModel { get; set; } = "gpt-4o-mini";

    [Required(ErrorMessage = "El modelo de razonamiento de Sensi es obligatorio.")]
    public string ReasoningModel { get; set; } = "gpt-4o";

    [Range(0.1, 100.0, ErrorMessage = "El límite de gasto total mensual debe estar entre 0.1 y 100 USD.")]
    public double TotalBudgetCapUsd { get; set; } = 5.00;

    [Range(0.1, 100.0, ErrorMessage = "El límite de gasto del modelo avanzado debe estar entre 0.1 y 100 USD.")]
    public double ReasoningBudgetCapUsd { get; set; } = 3.00;

    [Range(0.01, 1.0, ErrorMessage = "El umbral de alerta debe estar entre 0.01 y 1.0.")]
    public double AlertThresholdRatio { get; set; } = 0.80;

    [Range(10, 5000)]
    public int MaxInputChars { get; set; } = 1500;

    [Range(10, 2000)]
    public int MaxCompletionTokens { get; set; } = 500;

    [Range(1, 20)]
    public int MaxTurnsHistory { get; set; } = 6;

    [Range(1, 200)]
    public int DailyMessageLimitPerUser { get; set; } = 20;

    [Range(1, 5)]
    public int MaxAgenticRounds { get; set; } = 3;

    [Range(1, 29)]
    public int TimeoutSeconds { get; set; } = 25;

    [Range(0.0001, 1.0)]
    public decimal FastEstimatedCostUsd { get; set; } = 0.001m;

    [Range(0.0001, 1.0)]
    public decimal ReasoningEstimatedCostUsd { get; set; } = 0.016m;

    [Range(1, 365)]
    public int TraceRetentionDays { get; set; } = 30;

    public Dictionary<string, TextCategoryThreshold> TextModerationThresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
