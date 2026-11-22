using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Media.Delivery;
using Sensus.Domain.Common;

namespace Sensus.Api.Endpoints;

/// <summary>
/// Entrega de contenido multimedia mediante URLs firmadas de corta vigencia (Spec 023).
/// La emisión exige sesión; la entrega valida la firma y el estado en cada petición (fail-closed).
/// </summary>
public static class MediaDeliveryEndpoints
{
    public const string RoutePrefix = "/api/v1/media";

    /// <summary>
    /// Ref: FR-023-02 - emisión de la URL firmada; FR-023-03 - política de caché por privacidad.
    /// </summary>
    public static IEndpointRouteBuilder MapMediaDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup(RoutePrefix)
            .WithTags("Entrega de multimedia (Spec 023)");

        group.MapPost("/{id:guid}/delivery-url", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<MediaDeliveryUrlDto> result = await sender.Send(new CreateMediaDeliveryUrlCommand(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("CreateMediaDeliveryUrl")
        .WithSummary("Spec 023 (FR-023-02): emite una URL firmada de corta vigencia para entregar un activo multimedia")
        .Produces<MediaDeliveryUrlDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:guid}/delivery", async (
            Guid id,
            [FromQuery] long? expires,
            [FromQuery] string? signature,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<MediaDeliveryResponse> result = await sender.Send(
                new GetMediaDeliveryQuery(id, expires ?? 0, signature),
                ct);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            MediaDeliveryResponse delivery = result.Value;
            httpContext.Response.Headers.CacheControl = delivery.CacheControl;

            return delivery.IsRedirect && !string.IsNullOrWhiteSpace(delivery.RedirectUrl)
                ? Results.Redirect(delivery.RedirectUrl)
                : Results.Stream(delivery.Content!, delivery.ContentType);
        })
        .AllowAnonymous()
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetMediaDelivery")
        .WithSummary("Spec 023 (FR-023-01, FR-023-02): entrega el contenido validando la firma y el estado de moderación")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
