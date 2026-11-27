using Sensus.Application.Common.Moderation;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers;

/// <summary>
/// Verifica que la imagen de una oferta pertenezca al titular y haya sido aprobada
/// para el mismo título y descripción (soporta separador canónico y formato móvil <c>" - "</c>).
/// </summary>
internal static class OfferMediaApprovalHelper
{
    internal static async Task<bool> HasApprovedImageAsync(
        IMediaAssetRepository? mediaAssets,
        Guid ownerId,
        string imageUrl,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        if (mediaAssets is null)
        {
            return false;
        }

        foreach (string fingerprint in ModeratedTextFingerprint.CreateOfferCandidates(title, description))
        {
            if (await mediaAssets.HasApprovedImageAsync(ownerId, imageUrl, fingerprint, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
