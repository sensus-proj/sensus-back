namespace Sensus.Application.Common.Interfaces;

public interface IPushDispatcher
{
    Task<PushResult> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task<PushResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public enum PushResult
{
    Unavailable,
    Accepted,
    InvalidToken
}
