using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Ai.Commands.CreateConversation;
using Sensus.Application.Ai.Commands.DeleteConversation;
using Sensus.Application.Ai.Commands.SendMessage;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Ai.Queries.GetConversation;

namespace Sensus.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai")
            .WithTags("Sensi IA")
            .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy);

        // POST /api/v1/ai/conversations
        group.MapPost("/conversations", async (
            [FromBody] CreateConversationRequestDto? body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateOrResumeConversationCommand(
                body?.ForceNew ?? false,
                body?.ScreenContext);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.AiChatPolicy)
        .WithSummary("Crear una nueva conversación o recuperar la conversación activa de la persona autenticada")
        .Produces<ConversationSummaryResponseDto>(StatusCodes.Status200OK)
        .Produces<ConversationSummaryResponseDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // GET /api/v1/ai/conversations/{conversationId}
        group.MapGet("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var query = new GetConversationQuery(conversationId, page, pageSize);
            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.ReadHeavyPolicy)
        .WithSummary("Consultar una conversación propia con sus mensajes paginados")
        .Produces<ConversationDetailResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // DELETE /api/v1/ai/conversations/{conversationId}
        group.MapDelete("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DeleteConversationCommand(conversationId);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithSummary("Eliminar una conversación propia y purgar en cascada sus mensajes y trazas redactadas")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // POST /api/v1/ai/messages
        group.MapPost("/messages", async (
            [FromBody] SendAiMessageRequestDto body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new SendAiMessageCommand(
                body.ConversationId,
                body.Message,
                body.ScreenContext,
                body.Latitude,
                body.Longitude,
                body.RadiusKm);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(SecurityExtensions.AiChatPolicy)
        .WithSummary("Enviar un mensaje del usuario a Sensi IA a través de la arquitectura de 7 capas")
        .Produces<AiTurnResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
