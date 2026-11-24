namespace Sensus.Application.Common.Interfaces;

public interface IPublicationTextModerationService
{
    Task<ImageModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken = default);
}
