using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Comercios.Commands.RegisterComercio;

/// <summary>
/// SE-003 & SE-004: Comando para registrar el comercio y activar inmediatamente el rol comercial.
/// </summary>
public sealed record RegisterComercioCommand(
    string NombreComercial,
    string Direccion,
    string CodigoPostal,
    string? Categoria = null,
    double? Latitud = null,
    double? Longitud = null) : IRequest<Result<RegisterComercioResponseDto>>;
