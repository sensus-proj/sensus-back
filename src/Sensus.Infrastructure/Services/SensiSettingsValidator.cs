using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;

namespace Sensus.Infrastructure.Services;

public sealed class SensiSettingsValidator : IValidateOptions<SensiSettings>
{
    public ValidateOptionsResult Validate(string? name, SensiSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ModerationModel))
        {
            failures.Add("OpenAiSettings:Sensi:ModerationModel es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.FastModel))
        {
            failures.Add("OpenAiSettings:Sensi:FastModel es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.ReasoningModel))
        {
            failures.Add("OpenAiSettings:Sensi:ReasoningModel es obligatorio.");
        }

        if (options.TotalBudgetCapUsd <= 0 || options.TotalBudgetCapUsd > 100.0)
        {
            failures.Add("OpenAiSettings:Sensi:TotalBudgetCapUsd debe estar entre 0.1 y 100.0 USD.");
        }

        if (options.ReasoningBudgetCapUsd <= 0 || options.ReasoningBudgetCapUsd > options.TotalBudgetCapUsd)
        {
            failures.Add("OpenAiSettings:Sensi:ReasoningBudgetCapUsd debe ser mayor a 0 y no exceder TotalBudgetCapUsd.");
        }

        if (options.AlertThresholdRatio <= 0 || options.AlertThresholdRatio > 1.0)
        {
            failures.Add("OpenAiSettings:Sensi:AlertThresholdRatio debe estar entre 0.01 y 1.0.");
        }

        if (options.DailyMessageLimitPerUser <= 0)
        {
            failures.Add("OpenAiSettings:Sensi:DailyMessageLimitPerUser debe ser mayor a 0.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
