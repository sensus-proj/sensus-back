using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Models;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendsActivity;

namespace Sensus.Api.Endpoints;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/community")
            .WithTags("Comunidad")
            .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy);

        group.MapGet("/friends-activity", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] int? limit,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetFriendsCommunityActivityQuery(
                page ?? 1, pageSize ?? limit ?? 20), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetFriendsCommunityActivity")
        .WithSummary("Obtiene de forma paginada las reseñas, respuestas y me gusta de ofertas de tus amigos conectados y tuyos")
        .Produces<PagedResult<CommunityActivityItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
