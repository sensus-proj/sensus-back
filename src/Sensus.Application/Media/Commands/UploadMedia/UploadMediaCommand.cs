using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Media.Commands.UploadMedia;

/// <summary>
/// Spec 022: Comando para subir una imagen a través de multipart/form-data,
/// validar tipo y tamaño, almacenar en Cloudinary y moderar antes de exponerla.
/// </summary>
public sealed record UploadMediaCommand(
    byte[] ImageBytes,
    string ContentType,
    string? TextContext = null) : IRequest<Result<UploadMediaResponseDto>>;
