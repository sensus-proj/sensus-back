using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(
    Guid ConversationId) : IRequest<Result>;
