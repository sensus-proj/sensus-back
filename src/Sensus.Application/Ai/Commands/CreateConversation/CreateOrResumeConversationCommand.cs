using MediatR;
using Sensus.Application.Ai.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Commands.CreateConversation;

public sealed record CreateOrResumeConversationCommand(
    bool ForceNew = false,
    string? ScreenContext = null) : IRequest<Result<ConversationSummaryResponseDto>>;
