using Microsoft.Extensions.Options;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Valida al arranque el presupuesto temporal y límites de reintento de moderación con OpenAI (<c>NFR-017-03</c>).
/// </summary>
public sealed class OpenAiSettingsValidator : IValidateOptions<OpenAiSettings>
{
    public const double MaxModerationBudgetSeconds = 20.0;

    public ValidateOptionsResult Validate(string? name, OpenAiSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var retry = options.Moderation ?? new ModerationRetrySettings();

        if (options.TimeoutSeconds is <= 0 or > 20)
        {
            failures.Add("OpenAiSettings:TimeoutSeconds debe estar entre 1 y 20 segundos.");
        }

        if (retry.MaxRetries is < 0 or > 2)
        {
            failures.Add("OpenAiSettings:Moderation:MaxRetries debe estar entre 0 y 2.");
        }

        if (retry.RetryDelayMilliseconds is < 0 or > 2000)
        {
            failures.Add("OpenAiSettings:Moderation:RetryDelayMilliseconds debe estar entre 0 y 2000 ms.");
        }

        var budgetSeconds = options.TimeoutSeconds * (1 + retry.MaxRetries)
            + (retry.MaxRetries * (retry.RetryDelayMilliseconds / 1000.0));
        if (budgetSeconds > MaxModerationBudgetSeconds)
        {
            failures.Add(
                $"El presupuesto total de moderación ({budgetSeconds:0.##} s) excede el máximo de {MaxModerationBudgetSeconds:0} s.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
