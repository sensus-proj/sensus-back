using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Comercios.Commands.RegisterComercio;
using Sensus.Application.Comercios.Commands.UpdateMerchantProfile;
using Sensus.Application.Comercios.DTOs;

namespace Sensus.Api.Endpoints;

public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        MapMerchantGroup(app.MapGroup("/api/v1/merchants"), isV1: true);
        MapMerchantGroup(app.MapGroup("/api/merchants"), isV1: false); // Alias sin prefijo v1 según especificación

        // SE-003 & SE-004 / Catálogo #6: Activación de perfil comercial
        MapActivateMerchantEndpoint(app, "/api/v1/merchants/activate", isV1: true);
        MapActivateMerchantEndpoint(app, "/api/merchants/activate", isV1: false);

        return app;
    }

    private static void MapActivateMerchantEndpoint(IEndpointRouteBuilder app, string pattern, bool isV1)
    {
        var endpoint = app.MapPost(pattern, async (
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
        .WithTags("Comerciantes (SE-007)")
        .WithSummary("Catálogo #6: Activa el perfil comercial y registra el local")
        .Produces<RegisterComercioResponseDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        if (isV1)
        {
            endpoint.WithName("ActivateMerchantProfile");
        }
    }

    private static void MapMerchantGroup(RouteGroupBuilder group, bool isV1)
    {
        group.WithTags("Comerciantes (SE-007)")
            .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy);

        // SE-007: Actualización de datos del local comercial
        var update = group.MapPut("/profile", async (
            [FromBody] UpdateMerchantProfileCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithSummary("SE-007: Actualiza horarios y dirección del local comercial recalculando coordenadas espaciales")
        .Produces<ComercioDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        if (isV1) update.WithName("UpdateMerchantProfile");
    }
}
