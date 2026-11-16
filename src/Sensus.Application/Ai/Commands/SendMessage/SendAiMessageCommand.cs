using MediatR;
using Sensus.Application.Ai.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Commands.SendMessage;

public sealed record SendAiMessageCommand(
    string? ConversationId,
    string Message,
    string? ScreenContext = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null) : IRequest<Result<AiTurnResponseDto>>;
