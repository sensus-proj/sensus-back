using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.Commands.ClaimOffer;
using Sensus.Application.Offers.Commands.CreateOffer;
using Sensus.Application.Offers.Commands.ExpireClaims;
using Sensus.Application.Offers.Commands.ExpireOffers;
using Sensus.Application.Offers.Commands.RedeemClaim;
using Sensus.Application.Offers.Commands.RegisterOfferView;
using Sensus.Application.Offers.Commands.RepublishOffer;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetMerchantOffers;
using Sensus.Application.Offers.Queries.GetNearbyOffers;
using Sensus.Application.Offers.Queries.GetOfferDetail;

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

        // Spec 007: Ofertas publicadas dentro de un radio, ordenadas por distancia
        offersGroup.MapGet("/nearby", async (
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double? radiusKm,
            [FromQuery] string? category,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetNearbyOffersQuery(
                lat ?? double.NaN,
                lng ?? double.NaN,
                radiusKm,
                category,
                page ?? 1,
                pageSize ?? GetNearbyOffersQueryValidator.DefaultPageSize);

            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetNearbyOffers")
        .WithSummary("Spec 007: Lista ofertas publicadas cerca de la ubicación enviada, con distancia y paginación")
        .Produces<PagedResult<NearbyOfferDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 033: Apartado de una unidad con código firmado de un solo uso
        offersGroup.MapPost("/{id:guid}/claim", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ClaimOfferCommand(id);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("ClaimOffer")
        .WithSummary("Spec 033: Aparta una unidad de la oferta y devuelve el código firmado y su payload QR")
        .Produces<ClaimOfferResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 033: Canje del código por el comerciante propietario
        offersGroup.MapPost("/claims/{code}/redeem", async (
            string code,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RedeemClaimCommand(code);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.MerchantOnlyPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("RedeemClaim")
        .WithSummary("Spec 033: Canjea un código de apartado validando firma, vigencia y propiedad del comercio")
        .Produces<RedeemClaimResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 033: Disparador del job de expiración de apartados (EventBridge o llamada programada)
        offersGroup.MapPost("/claims/expire-check", async (
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ExpireClaimsCommand();
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("TriggerClaimExpiration")
        .WithSummary("Spec 033: Expira los apartados vencidos y devuelve el stock reservado")
        .Produces<ExpireClaimsResultDto>(StatusCodes.Status200OK);

        // Spec 032: Detalle completo de la oferta con datos públicos del comercio
        offersGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetOfferDetailQuery(id);
            var result = await sender.Send(query, ct);
            return result.IsFailure ? result.ToHttpResult() : Results.Ok(result.Value);
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetOfferDetail")
        .WithSummary("Spec 032: Detalle de la oferta con comercio, vigencia, stock, precio y apartado activo")
        .Produces<OfferDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 032: Registro de vista deduplicado por usuario y día
        offersGroup.MapPost("/{id:guid}/view", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RegisterOfferViewCommand(id);
            var result = await sender.Send(command, ct);
            return result.IsFailure ? result.ToHttpResult() : Results.NoContent();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithName("RegisterOfferView")
        .WithSummary("Spec 032: Registra una vista por usuario, oferta y día; responde 204 sin datos internos")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

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
