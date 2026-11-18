using MediatR;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Ai.Commands.CreateConversation;

public sealed class CreateOrResumeConversationCommandHandler
    : IRequestHandler<CreateOrResumeConversationCommand, Result<ConversationSummaryResponseDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAiConversationRepository _conversationRepository;
    private readonly IKnowledgeBaseProvider _knowledgeBaseProvider;
    private readonly IAiModelRouter _modelRouter;

    public CreateOrResumeConversationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAiConversationRepository conversationRepository,
        IKnowledgeBaseProvider knowledgeBaseProvider,
        IAiModelRouter modelRouter)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
        _knowledgeBaseProvider = knowledgeBaseProvider;
        _modelRouter = modelRouter;
    }

    public async Task<Result<ConversationSummaryResponseDto>> Handle(
        CreateOrResumeConversationCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<ConversationSummaryResponseDto>(
                Error.Unauthorized("Ai.Unauthorized", "Se requiere autenticación para acceder a Sensi IA."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Estatus)
        {
            return Result.Failure<ConversationSummaryResponseDto>(
                Error.Unauthorized("Ai.UserNotFound", "El usuario no existe o se encuentra inactivo."));
        }

        if (user.HasRole(RoleType.Invitado))
        {
            return Result.Failure<ConversationSummaryResponseDto>(
                Error.Forbidden("Ai.GuestForbidden", "Las sesiones de invitado no tienen acceso a Sensi IA."));
        }

        var validatedScreen = KnownScreenContexts.Normalize(request.ScreenContext);

        if (!request.ForceNew)
        {
            var existing = await _conversationRepository.GetActiveByUserIdAsync(userId, cancellationToken);
            if (existing is not null)
            {
                if (validatedScreen != null &&
                    !string.Equals(existing.LastScreenContext, validatedScreen, StringComparison.Ordinal))
                {
                    existing.UpdateScreenContext(validatedScreen, DateTime.UtcNow);
                    await _conversationRepository.UpdateAsync(existing, cancellationToken);
                }

                return Result.Success(new ConversationSummaryResponseDto(
                    existing.Id.ToString(),
                    existing.Status.ToString(),
                    existing.AssignedModel,
                    existing.RoutingTier.ToString(),
                    existing.MessageCount,
                    existing.LastScreenContext,
                    existing.MarkdownVersion,
                    existing.CreatedAtUtc,
                    existing.LastModifiedAtUtc));
            }
        }

        var initialRouting = _modelRouter.RouteInitialTurn(string.Empty);
        var knowledgeDoc = _knowledgeBaseProvider.GetDocument();
        var now = DateTime.UtcNow;

        var activeRole = user.HasRole(RoleType.Comerciante) ? RoleType.Comerciante : RoleType.Consumidor;
        var createResult = AiConversation.Create(
            userId,
            activeRole,
            initialRouting.SelectedModel,
            initialRouting.Tier,
            knowledgeDoc.Version,
            validatedScreen,
            now);

        if (createResult.IsFailure)
        {
            return Result.Failure<ConversationSummaryResponseDto>(createResult.Error);
        }

        var conversation = createResult.Value;
        await _conversationRepository.AddAsync(conversation, cancellationToken);

        return Result.Success(new ConversationSummaryResponseDto(
            conversation.Id.ToString(),
            conversation.Status.ToString(),
            conversation.AssignedModel,
            conversation.RoutingTier.ToString(),
            conversation.MessageCount,
            conversation.LastScreenContext,
            conversation.MarkdownVersion,
            conversation.CreatedAtUtc,
            conversation.LastModifiedAtUtc));
    }
}
