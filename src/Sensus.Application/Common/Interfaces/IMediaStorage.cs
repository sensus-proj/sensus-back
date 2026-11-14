using Sensus.Domain.Common;

namespace Sensus.Application.Common.Interfaces;

public sealed record MediaUploadResult(
    string PublicId,
    string? SecureUrl,
    string? ErrorMessage = null);

public interface IMediaStorage
{
    Task<Result<MediaUploadResult>> UploadImageAsync(
        byte[] imageBytes,
        string contentType,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default);
}
