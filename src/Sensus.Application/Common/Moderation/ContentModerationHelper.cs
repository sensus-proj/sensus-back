using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;

namespace Sensus.Application.Common.Moderation;

/// <summary>
/// Helper compartido para moderación preventiva (fail-closed) de textos de usuario, comercio y ofertas.
/// </summary>
public static class ContentModerationHelper
{
    public static async Task<Result> EnsureApprovedAsync(
        ITextModerationService? moderationService,
        string? text,
        string contentContext,
        string errorCode,
        string defaultRejectionMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Success();
        }

        if (ProfanityFilter.ContainsProfanity(text))
        {
            return Result.Failure(Error.Validation(errorCode, ProfanityFilter.DefaultRejectionMessage));
        }

        if (moderationService is null)
        {
            return Result.Success();
        }

        TextModerationResult? verdict = await moderationService.ModerateContentTextAsync(
            text,
            contentContext,
            cancellationToken);

        if (verdict is { IsApproved: true })
        {
            return Result.Success();
        }

        string message = !string.IsNullOrWhiteSpace(verdict?.Message)
            ? verdict.Message
            : defaultRejectionMessage;

        return Result.Failure(Error.Validation(errorCode, message));
    }
}
