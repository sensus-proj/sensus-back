using MediatR;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Ai.Queries.GetConversation;

public sealed class GetConversationQueryHandler
    : IRequestHandler<GetConversationQuery, Result<ConversationDetailResponseDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiConversationRepository _conversationRepository;
    private readonly IAiMessageRepository _messageRepository;

    public GetConversationQueryHandler(
        ICurrentUserService currentUserService,
        IAiConversationRepository conversationRepository,
        IAiMessageRepository messageRepository)
    {
        _currentUserService = currentUserService;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
    }

    public async Task<Result<ConversationDetailResponseDto>> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<ConversationDetailResponseDto>(
                Error.Unauthorized("Ai.Unauthorized", "Se requiere autenticación para consultar conversaciones."));
        }

        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || conversation.OwnerUserId != userId)
        {
            return Result.Failure<ConversationDetailResponseDto>(
                Error.NotFound("Ai.ConversationNotFound", "La conversación no existe o no pertenece al usuario autenticado."));
        }

        var messages = await _messageRepository.GetPagedByConversationIdAsync(
            conversation.Id,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalMessages = await _messageRepository.CountByConversationIdAsync(conversation.Id, cancellationToken);

        var messageDtos = messages.Select(m => new ConversationMessageItemDto(
            m.Id.ToString(),
            m.Role.ToString(),
            m.RedactedContent,
            m.RecommendedPackage is not null ? MapPackageDto(m.RecommendedPackage) : null,
            null,
            m.ToolsInvoked,
            m.Degraded,
            m.CreatedAtUtc)).ToList();

        var summary = new ConversationSummaryResponseDto(
            conversation.Id.ToString(),
            conversation.Status.ToString(),
            conversation.AssignedModel,
            conversation.RoutingTier.ToString(),
            conversation.MessageCount,
            conversation.LastScreenContext,
            conversation.MarkdownVersion,
            conversation.CreatedAtUtc,
            conversation.LastModifiedAtUtc);

        var totalPages = (int)Math.Ceiling((double)totalMessages / Math.Max(1, request.PageSize));
        var pagination = new PaginationMetadataDto(request.Page, request.PageSize, totalMessages, totalPages);

        return Result.Success(new ConversationDetailResponseDto(
            summary,
            messageDtos,
            pagination));
    }

    private static RecommendedPackageDto MapPackageDto(Sensus.Domain.ValueObjects.RecommendedPackage pkg)
    {
        var items = pkg.Items.Select(i => new CitedOfferDto(
            i.OfferId,
            i.Title,
            i.MerchantId,
            i.MerchantName,
            i.OfferPriceMxn,
            i.RegularPriceMxn,
            i.AvailableStock,
            i.IsOpenNow,
            i.DistanceKm)).ToList();

        return new RecommendedPackageDto(
            pkg.BudgetMxn,
            pkg.TotalPriceMxn,
            pkg.SavingsMxn,
            items);
    }
}
