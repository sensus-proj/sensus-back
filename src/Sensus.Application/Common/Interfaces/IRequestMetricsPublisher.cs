namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Publica las métricas de aplicación de cada solicitud HTTP (Spec 015, FR-015-01).
/// Solo recibe el código de estado y la latencia: nunca rutas, identificadores ni datos personales.
/// </summary>
public interface IRequestMetricsPublisher
{
    void PublishRequest(int statusCode, double elapsedMilliseconds);
}
