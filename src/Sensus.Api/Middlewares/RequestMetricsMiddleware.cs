using System.Diagnostics;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Api.Middlewares;

/// <summary>
/// Mide la latencia y el código de estado final de cada solicitud y los entrega al publicador de métricas
/// (Spec 015, FR-015-01). Se registra antes del manejador de excepciones para ver también los 500 finales.
/// </summary>
public sealed class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRequestMetricsPublisher _publisher;

    public RequestMetricsMiddleware(RequestDelegate next, IRequestMetricsPublisher publisher)
    {
        _next = next;
        _publisher = publisher;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        catch
        {
            // Una excepción que escapa del pipeline termina como 500 en el host.
            _publisher.PublishRequest(StatusCodes.Status500InternalServerError, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }

        _publisher.PublishRequest(context.Response.StatusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
