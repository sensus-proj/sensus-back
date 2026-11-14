using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.Comercios.Commands.UpdateMerchantProfile;

public sealed class UpdateMerchantProfileCommandHandler : IRequestHandler<UpdateMerchantProfileCommand, Result<ComercioDto>>
{
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGeocodingService _geocodingService;
    private readonly ICacheService? _cacheService;
    private readonly IOfferRepository? _offerRepository;

    public UpdateMerchantProfileCommandHandler(
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IGeocodingService geocodingService,
        ICacheService? cacheService = null,
        IOfferRepository? offerRepository = null)
    {
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _geocodingService = geocodingService;
        _cacheService = cacheService;
        _offerRepository = offerRepository;
    }

    public async Task<Result<ComercioDto>> Handle(UpdateMerchantProfileCommand request, CancellationToken cancellationToken)
    {
        var rawUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var usuarioId))
        {
            return Result.Failure<ComercioDto>(
                Error.Unauthorized("Auth.Unauthorized", "Debe iniciar sesión para actualizar el local comercial."));
        }

        var comercio = await _comercioRepository.GetByUsuarioIdAsync(usuarioId, cancellationToken);
        if (comercio is null)
        {
            return Result.Failure<ComercioDto>(
                Error.NotFound("Comercio.NotFound", "No se encontró ningún local comercial vinculado a este usuario."));
        }

        var nombreComercial = !string.IsNullOrWhiteSpace(request.NombreComercial)
            ? request.NombreComercial
            : comercio.NombreComercial;

        var direccionCambio = !string.Equals(comercio.Direccion, request.Direccion, StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(comercio.CodigoPostal, request.CodigoPostal, StringComparison.OrdinalIgnoreCase);

        var coordsResult = await ComercioCoordinateHelper.ResolveAsync(
            request.Latitud,
            request.Longitud,
            shouldGeocodeIfMissing: direccionCambio,
            request.Direccion,
            request.CodigoPostal,
            _geocodingService,
            cancellationToken);

        if (coordsResult.IsFailure)
        {
            return Result.Failure<ComercioDto>(coordsResult.Error);
        }

        var (latitud, longitud) = coordsResult.Value ?? (comercio.Latitud, comercio.Longitud);

        var updateResult = comercio.UpdateDetails(
            nombreComercial,
            request.Direccion,
            request.CodigoPostal,
            latitud,
            longitud,
            ParseCategoria(request.Categoria));

        if (updateResult.IsFailure)
        {
            return Result.Failure<ComercioDto>(updateResult.Error);
        }

        // Actualizar horarios de atención si se proporcionaron
        if (request.Horarios is not null)
        {
            var domainHorarios = new List<HorarioDia>();
            foreach (var h in request.Horarios)
            {
                if (h.EstaCerrado)
                {
                    domainHorarios.Add(HorarioDia.Cerrado(h.Dia).Value);
                }
                else
                {
                    if (!TimeOnly.TryParse(h.HoraApertura, out var apertura) ||
                        !TimeOnly.TryParse(h.HoraCierre, out var cierre))
                    {
                        return Result.Failure<ComercioDto>(
                            Error.Validation("Horario.FormatoInvalido", $"El formato de hora para el día {h.Dia} debe ser HH:mm."));
                    }

                    var horarioResult = HorarioDia.Abierto(h.Dia, apertura, cierre);
                    if (horarioResult.IsFailure)
                    {
                        return Result.Failure<ComercioDto>(horarioResult.Error);
                    }
                    domainHorarios.Add(horarioResult.Value);
                }
            }

            var horariosResult = comercio.UpdateHorarios(domainHorarios);
            if (horariosResult.IsFailure)
            {
                return Result.Failure<ComercioDto>(horariosResult.Error);
            }
        }

        await _comercioRepository.UpdateAsync(comercio, cancellationToken);

        if (_offerRepository is not null)
        {
            await _offerRepository.UpdateMerchantLocationAsync(comercio.Id, latitud, longitud, cancellationToken);
        }

        // Invalidar caché del comercio y ofertas cercanas (Spec 048)
        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync($"sensus:merchants:user:{usuarioId}", cancellationToken);
            await _cacheService.RemoveAsync($"sensus:merchants:{comercio.Id}", cancellationToken);
            await _cacheService.InvalidateAsync(null, cancellationToken);
        }

        return Result.Success(comercio.ToDto());
    }

    private static MerchantCategory? ParseCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return null;
        }

        return MerchantCategoryCatalog.TryParse(categoria, out var parsed) ? parsed : null;
    }
}
