using MediatR;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Media.Commands.ModerateImage;

/// <summary>
/// Comando para moderación multimodal de imágenes antes de su publicación o actualización con OpenAI Omni-Moderation.
/// </summary>
public sealed record ModerateImageCommand(
    string ImageBase64,
    string? ContentType = "image/jpeg") : IRequest<Result<ModerateImageResponseDto>>;
