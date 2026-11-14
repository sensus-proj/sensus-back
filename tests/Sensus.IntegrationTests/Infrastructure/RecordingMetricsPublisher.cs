using System.Collections.Concurrent;
using Sensus.Application.Common.Interfaces;

namespace Sensus.IntegrationTests.Infrastructure;

/// <summary>
/// Doble del publicador de métricas que guarda cada solicitud medida (Spec 015).
/// </summary>
public sealed class RecordingMetricsPublisher : IRequestMetricsPublisher
{
    private readonly ConcurrentQueue<(int StatusCode, double ElapsedMilliseconds)> _requests = new();

    public IReadOnlyCollection<(int StatusCode, double ElapsedMilliseconds)> Requests => _requests.ToArray();

    public void PublishRequest(int statusCode, double elapsedMilliseconds)
    {
        _requests.Enqueue((statusCode, elapsedMilliseconds));
    }
}
