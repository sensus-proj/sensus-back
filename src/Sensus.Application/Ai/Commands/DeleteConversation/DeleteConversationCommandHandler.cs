using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Ai.Commands.DeleteConversation;

public sealed class DeleteConversationCommandHandler : IRequestHandler<DeleteConversationCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiConversationRepository _conversationRepository;

    public DeleteConversationCommandHandler(
        ICurrentUserService currentUserService,
        IAiConversationRepository conversationRepository)
    {
        _currentUserService = currentUserService;
        _conversationRepository = conversationRepository;
    }

    public async Task<Result> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure(
                Error.Unauthorized("Ai.Unauthorized", "Se requiere autenticación para eliminar conversaciones."));
        }

        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || conversation.OwnerUserId != userId)
        {
            return Result.Failure(
                Error.NotFound("Ai.ConversationNotFound", "La conversación no existe o no pertenece al usuario autenticado."));
        }

        await _conversationRepository.DeleteAsync(conversation.Id, cancellationToken);
        return Result.Success();
    }
}
