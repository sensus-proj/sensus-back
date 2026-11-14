using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Media.Commands.ModerateImage;

public sealed class ModerateImageCommandHandler : IRequestHandler<ModerateImageCommand, Result<ModerateImageResponseDto>>
{
    private readonly IImageModerationService _moderationService;

    public ModerateImageCommandHandler(IImageModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public async Task<Result<ModerateImageResponseDto>> Handle(
        ModerateImageCommand request,
        CancellationToken cancellationToken)
    {
        var rawBase64 = request.ImageBase64.Trim();
        if (rawBase64.Contains(','))
        {
            rawBase64 = rawBase64[(rawBase64.IndexOf(',') + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rawBase64);
        }
        catch (FormatException)
        {
            return Result.Failure<ModerateImageResponseDto>(
                Error.Validation("Media.InvalidBase64", "El formato Base64 de la imagen es inválido."));
        }

        var result = await _moderationService.ModerateImageBytesAsync(
            bytes,
            request.ContentType ?? "image/jpeg",
            request.TextContext,
            cancellationToken);

        var dto = new ModerateImageResponseDto(
            Approved: result.IsApproved,
            Message: result.Message ?? (result.IsApproved
                ? "Imagen aprobada exitosamente."
                : "No se pudo subir la imagen por políticas de seguridad."),
            FlaggedCategories: result.FlaggedCategories,
            Decision: result.Decision,
            CategoryScores: result.CategoryScores);

        return Result.Success(dto);
    }
}
