using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Offers.Commands.CreateOffer;
using Sensus.Application.Offers.Commands.ExpireOffers;
using Sensus.Application.Offers.Commands.RepublishOffer;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetMerchantOffers;

namespace Sensus.Api.Endpoints;

public static class OfferEndpoints
{
    public static IEndpointRouteBuilder MapOfferEndpoints(this IEndpointRouteBuilder app)
    {
        // Grupo REST para ofertas: /api/v1/offers
        var offersGroup = app.MapGroup("/api/v1/offers")
            .WithTags("Ofertas (SE-022 a SE-026)");

        // SE-022 y Spec 034: Creación de oferta con fecha de expiración automática
        offersGroup.MapPost("/", async (
            [FromBody] CreateOfferCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            return Results.Created($"/api/v1/offers/{result.Value.Id}", result.Value);
        })
        .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy)
        .WithName("CreateOffer")
        .WithSummary("SE-022: Crea una nueva oferta estableciendo fecha y hora de vencimiento automática")
        .Produces<OfferDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // SE-026 y Spec 036: Republicación rápida de ofertas pasadas (Expiradas / Archivadas)
        offersGroup.MapPost("/{id:guid}/republish", async (
            Guid id,
            [FromBody] RepublishOfferBody body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RepublishOfferCommand(id, body.NewStartsAt, body.NewEndsAt, body.NewStock);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy)
        .WithName("RepublishOffer")
        .WithSummary("SE-026: Marca y republica una oferta expirada o archivada con nuevas fechas de vigencia y stock")
        .Produces<OfferDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // SE-022 a SE-025: Disparador de expiración automática (Sustituto de Trigger/Job SQL para AWS EventBridge o llamadas programadas)
        offersGroup.MapPost("/expire-check", async (
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ExpireOffersCommand();
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("TriggerOfferExpiration")
        .WithSummary("SE-022 a SE-025: Ejecuta la transición atómica en MongoDB de ofertas cuya vigencia expiró")
        .Produces<ExpireOffersResultDto>(StatusCodes.Status200OK);

        // SE-026 y Spec 037: Listado de ofertas propias del comercio con filtro opcional por estado
        var merchantGroup = app.MapGroup("/api/v1/merchants")
            .WithTags("Ofertas (SE-022 a SE-026)")
            .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy);

        merchantGroup.MapGet("/my-offers", async (
            [FromQuery] string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetMerchantOffersQuery(status);
            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyOffers")
        .WithSummary("SE-026 y Spec 037: Lista las ofertas del comercio propio (activas o pasadas) para su gestión o republicación")
        .Produces<IReadOnlyList<OfferDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}

public sealed record RepublishOfferBody(
    DateTime NewStartsAt,
    DateTime NewEndsAt,
    int NewStock);
