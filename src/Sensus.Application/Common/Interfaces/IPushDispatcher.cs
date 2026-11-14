namespace Sensus.Application.Common.Interfaces;

public interface IPushDispatcher
{
    Task<bool> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}
