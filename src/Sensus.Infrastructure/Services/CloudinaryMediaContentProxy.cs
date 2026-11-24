using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Lee contenido aprobado desde el origen Cloudinary para servirlo a través de la API (Spec 023).
/// Solo acepta orígenes HTTPS del host de Cloudinary para impedir SSRF (Principio IV),
/// con timeout configurable y límite estricto de tamaño de respuesta (M2).
/// Ref: FR-023-01 - entrega segura de contenido; FR-023-02 - entrega controlada por backend.
/// </summary>
public sealed class CloudinaryMediaContentProxy : IMediaContentProxy
{
    /// <summary>Host permitido para el origen del contenido.</summary>
    public const string AllowedHost = "res.cloudinary.com";

    private readonly HttpClient _httpClient;
    private readonly IOptions<MediaDeliverySettings> _settings;
    private readonly ILogger<CloudinaryMediaContentProxy> _logger;

    public CloudinaryMediaContentProxy(
        HttpClient httpClient,
        IOptions<MediaDeliverySettings> settings,
        ILogger<CloudinaryMediaContentProxy> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MediaContentPayload>> OpenReadAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!TryResolveSecureUri(asset.SecureUrl, out Uri? uri))
        {
            _logger.LogWarning(
                "Se rechazó la lectura del activo {MediaId}: la URL de origen no es un origen HTTPS permitido.",
                asset.Id);
            return Result.Failure<MediaContentPayload>(
                Error.Unavailable("MediaDelivery.ContentUnavailable", "El contenido multimedia no está disponible."));
        }

        int timeoutSeconds = Math.Max(1, _settings.Value.ProxyTimeoutSeconds);
        long maxSizeBytes = Math.Max(1024, _settings.Value.MaxResponseSizeBytes);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "El origen multimedia respondió {StatusCode} para el activo {MediaId}.",
                    (int)response.StatusCode,
                    asset.Id);
                response.Dispose();
                return Result.Failure<MediaContentPayload>(
                    Error.Unavailable("MediaDelivery.ContentUnavailable", "El contenido multimedia no está disponible."));
            }

            if (response.Content.Headers.ContentLength.HasValue &&
                response.Content.Headers.ContentLength.Value > maxSizeBytes)
            {
                _logger.LogWarning(
                    "El activo {MediaId} excede el tamaño máximo permitido por proxy ({Size} > {Max}).",
                    asset.Id,
                    response.Content.Headers.ContentLength.Value,
                    maxSizeBytes);
                response.Dispose();
                return Result.Failure<MediaContentPayload>(
                    Error.Validation("MediaDelivery.PayloadTooLarge", "El contenido multimedia excede el tamaño máximo permitido."));
            }

            Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var boundedStream = new ResponseOwnedStream(stream, response, maxSizeBytes);
            return Result.Success(new MediaContentPayload(boundedStream, asset.ContentType));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tiempo de espera agotado ({Timeout}s) al leer el activo {MediaId} desde el origen.", timeoutSeconds, asset.Id);
            return Result.Failure<MediaContentPayload>(
                Error.Unavailable("MediaDelivery.ProxyTimeout", "Se agotó el tiempo de espera al obtener el contenido multimedia."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer el contenido del activo {MediaId} desde el origen.", asset.Id);
            return Result.Failure<MediaContentPayload>(
                Error.Unavailable("MediaDelivery.ContentUnavailable", "El contenido multimedia no está disponible."));
        }
    }

    private static bool TryResolveSecureUri(string? sourceUrl, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(sourceUrl)
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parsed.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// Envuelve el flujo del origen, valida el límite máximo de bytes leídos y libera la respuesta HTTP al cerrarse.
    /// </summary>
    private sealed class ResponseOwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;
        private readonly long _maxBytes;
        private long _bytesRead;
        private bool _disposed;

        public ResponseOwnedStream(Stream inner, HttpResponseMessage response, long maxBytes)
        {
            _inner = inner;
            _response = response;
            _maxBytes = maxBytes;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            CheckLimit(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            CheckLimit(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken);
            CheckLimit(read);
            return read;
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    _response.Dispose();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _inner.DisposeAsync();
                _response.Dispose();
                _disposed = true;
            }

            await base.DisposeAsync();
        }

        private void CheckLimit(int additionalBytes)
        {
            if (additionalBytes > 0)
            {
                _bytesRead += additionalBytes;
                if (_bytesRead > _maxBytes)
                {
                    throw new InvalidOperationException($"El flujo de respuesta excedió el límite configurado de {_maxBytes} bytes.");
                }
            }
        }
    }
}
