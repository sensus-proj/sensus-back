using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Friends.Commands.AcceptFriendRequest;
using Sensus.Application.Friends.Commands.RejectFriendRequest;
using Sensus.Application.Friends.Commands.RemoveFriend;
using Sensus.Application.Friends.Commands.SendFriendRequest;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendsOverview;
using Sensus.Domain.Common;

namespace Sensus.Api.Endpoints;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/friends")
            .WithTags("Amigos y Solicitudes (QR / Invitaciones)");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            Result<FriendsOverviewDto> result = await sender.Send(new GetFriendsOverviewQuery(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("GetFriendsOverview")
        .WithSummary("Obtiene amigos conectados, solicitudes pendientes recibidas y solicitudes enviadas")
        .Produces<FriendsOverviewDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/requests", async (
            [FromBody] SendFriendRequestInput input,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<SendFriendRequestResponseDto> result = await sender.Send(
                new SendFriendRequestCommand(input.InviteCode, input.AddedVia),
                ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("SendFriendRequest")
        .WithSummary("Envía una solicitud de amistad por código de invitación o QR")
        .Produces<SendFriendRequestResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/requests/{requestId:guid}/accept", async (
            Guid requestId,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<FriendItemDto> result = await sender.Send(new AcceptFriendRequestCommand(requestId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("AcceptFriendRequest")
        .WithSummary("Acepta una solicitud de amistad pendiente")
        .Produces<FriendItemDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/requests/{requestId:guid}/reject", async (
            Guid requestId,
            ISender sender,
            CancellationToken ct) =>
        {
            Result result = await sender.Send(new RejectFriendRequestCommand(requestId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("RejectFriendRequest")
        .WithSummary("Rechaza o cancela una solicitud de amistad pendiente")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{friendUserId:guid}", async (
            Guid friendUserId,
            ISender sender,
            CancellationToken ct) =>
        {
            Result result = await sender.Send(new RemoveFriendCommand(friendUserId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithName("RemoveFriend")
        .WithSummary("Elimina la conexión con un amigo")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
