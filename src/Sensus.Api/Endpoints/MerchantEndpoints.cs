using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Comercios.Commands.RegisterComercio;
using Sensus.Application.Comercios.Commands.UpdateMerchantProfile;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Comercios.Queries.GetMyComercio;
using Sensus.Application.Comercios.Queries.GetNearbyComercios;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Contrato vigente de comercios bajo <c>/api/v1/merchants</c> (Specs 005, 006, 007 y 008; ADR 0008).
/// Las rutas legacy en español (<c>/comercios</c>, <c>/comercio</c>) y los alias sin <c>/v1</c> fueron retirados.
/// </summary>
public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/merchants")
            .WithTags("Comerciantes (SE-007)");

        // SE-003 & SE-004 / Catálogo #6: Activación de perfil comercial
        group.MapPost("/activate", async (
            [FromBody] RegisterComercioCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            return Results.Created($"/api/v1/merchants/{result.Value.ComercioId}", result.Value);
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("ActivateMerchantProfile")
        .WithSummary("Catálogo #6: Activa el perfil comercial y registra el local")
        .Produces<RegisterComercioResponseDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // SE-004 / Spec 006: Consulta de datos del comercio propio (CA-006-03 devuelve 404 si el usuario no tiene comercio)
        group.MapGet("/me", async (
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyComercioQuery(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("GetMyMerchant")
        .WithSummary("SE-004: Obtiene los datos del comercio propio del usuario autenticado")
        .Produces<ComercioDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // SE-007: Actualización de datos del local comercial
        group.MapPut("/profile", async (
            [FromBody] UpdateMerchantProfileCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy)
        .WithName("UpdateMerchantProfile")
        .WithSummary("SE-007: Actualiza horarios y dirección del local comercial recalculando coordenadas espaciales")
        .Produces<ComercioDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // SE-008: Búsqueda de comercios cercanos por radio (máximo 2 km para invitados)
        group.MapGet("/nearby", async (
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] int? radioMetros,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNearbyComerciosQuery(lat, lng, radioMetros), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .WithName("GetNearbyMerchants")
        .WithSummary("SE-008: Consulta comercios por proximidad espacial (restringido a 2 km para invitados)")
        .Produces<IReadOnlyList<ComercioDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
