using MediatR;
using Sensus.Application.Ai.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Queries.GetConversation;

public sealed record GetConversationQuery(
    Guid ConversationId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<ConversationDetailResponseDto>>;
