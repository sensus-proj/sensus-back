using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendsActivity;

namespace Sensus.Api.Endpoints;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/community")
            .WithTags("Comunidad Social")
            .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy);

        group.MapGet("/friends-activity", async (
            [FromQuery] int? limit,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFriendsCommunityActivityQuery(limit ?? 50), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetFriendsCommunityActivity")
        .WithSummary("Obtiene únicamente los comentarios y likes de productos de tus amigos conectados y tuyos")
        .Produces<IReadOnlyList<CommunityActivityItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
