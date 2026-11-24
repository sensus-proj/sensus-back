using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Commands.ReplyStoreReview;

/// <summary>
/// Agrega una respuesta moderada a una reseña existente de un comercio (Spec 021, B3).
/// Ref: FR-021-01 - auditoría obligatoria de cada decisión de moderación (fail-closed).
/// </summary>
public sealed record ReplyStoreReviewCommand(Guid ReviewId, string Comment) : IRequest<Result<StoreReviewDto>>;

public sealed class ReplyStoreReviewCommandHandler
    : IRequestHandler<ReplyStoreReviewCommand, Result<StoreReviewDto>>
{
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITextModerationService? _textModerationService;
    private readonly IModerationAuditRecorder _auditRecorder;

    public ReplyStoreReviewCommandHandler(
        IStoreReviewRepository storeReviewRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IModerationAuditRecorder auditRecorder,
        ITextModerationService? textModerationService = null)
    {
        _storeReviewRepository = storeReviewRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _auditRecorder = auditRecorder;
        _textModerationService = textModerationService;
    }

    public async Task<Result<StoreReviewDto>> Handle(
        ReplyStoreReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest || !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<StoreReviewDto>(
                Error.Unauthorized("StoreReviewReply.Unauthorized", "Debes iniciar sesión para responder un comentario."));
        }

        StoreReview? review = await _storeReviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<StoreReviewDto>(
                Error.NotFound("StoreReview.NotFound", "La reseña solicitada no existe."));
        }

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<StoreReviewDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
            _textModerationService,
            request.Comment,
            "Respuesta a comentario de comercio",
            "StoreReviewReply.ContentModerationRejected",
            ProfanityFilter.DefaultRejectionMessage,
            cancellationToken);

        var auditEntry = new ModerationAuditEntry(
            review.Id,
            ModerationTargetType.StoreReview,
            evaluation.Verdict,
            evaluation.Decision,
            "store_review_reply",
            evaluation.FlaggedCategories,
            evaluation.CategoryScores,
            ActorId: userId.ToString(),
            ActorType: ModerationActorType.User);

        if (!evaluation.IsApproved)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);
            return Result.Failure<StoreReviewDto>(evaluation.RejectionError ?? Error.Validation("StoreReviewReply.ContentModerationRejected", "Contenido rechazado por moderación."));
        }

        Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
        if (auditResult.IsFailure)
        {
            return Result.Failure<StoreReviewDto>(auditResult.Error);
        }

        Result<StoreReviewReply> replyResult = review.AddReply(
            user.Id,
            user.NombreCompleto,
            user.FotoPerfilUrl,
            request.Comment,
            _dateTimeProvider.UtcNow);

        if (replyResult.IsFailure)
        {
            return Result.Failure<StoreReviewDto>(replyResult.Error);
        }

        await _storeReviewRepository.UpdateAsync(review, cancellationToken);

        return Result.Success(review.ToDto(user.Id));
    }
}
