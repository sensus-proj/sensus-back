using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

public sealed class SimulatedPushDispatcher : IPushDispatcher
{
    public Task<PushResult> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default) =>
        ValidateTokenAsync(token, cancellationToken);

    public Task<PushResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!string.IsNullOrWhiteSpace(token) && token.Length >= 20
            ? PushResult.Accepted
            : PushResult.InvalidToken);
    }
}
