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
        app.MapGet("/api/v1/community/friends-activity", async (
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
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithName("GetFriendsCommunityActivity")
        .WithTags("Comunidad")
        .Produces<PagedResult<CommunityActivityItemDto>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
