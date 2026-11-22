using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Moderation.Audit;

/// <summary>
/// Consulta paginada de la auditoría de moderación, exclusiva del rol Administrador (Spec 021).
/// Ref: FR-021-03 - consulta paginada por administrador; CA-021-03 - 403 sin permisos.
/// </summary>
public sealed record GetModerationAuditQuery(
    Guid? TargetId = null,
    string? TargetType = null,
    string? Verdict = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize)
    : IRequest<Result<PagedResult<ModerationAuditRecordDto>>>;

/// <summary>Ref: FR-021-03 - validación de filtros y límites de paginación (20 por defecto, 100 máximo).</summary>
public sealed class GetModerationAuditQueryValidator : AbstractValidator<GetModerationAuditQuery>
{
    public GetModerationAuditQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationPolicy.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationPolicy.MaxPageSize}.");

        RuleFor(x => x.TargetType)
            .Must(value => Enum.TryParse<ModerationTargetType>(value, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.TargetType))
            .WithMessage("El tipo de objetivo debe ser MediaAsset, FeedComment, Offer, StoreReview, Comercio o User.");

        RuleFor(x => x.Verdict)
            .Must(value => Enum.TryParse<ModerationVerdict>(value, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Verdict))
            .WithMessage("El veredicto debe ser Approved, Quarantined o Rejected.");

        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("La fecha final no puede ser anterior a la fecha inicial.");
    }
}

/// <summary>Ref: FR-021-03 - handler de la consulta administrativa paginada.</summary>
public sealed class GetModerationAuditQueryHandler
    : IRequestHandler<GetModerationAuditQuery, Result<PagedResult<ModerationAuditRecordDto>>>
{
    private readonly IModerationAuditRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetModerationAuditQueryHandler(
        IModerationAuditRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ModerationAuditRecordDto>>> Handle(
        GetModerationAuditQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.Roles.Contains(RoleType.Administrador, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<PagedResult<ModerationAuditRecordDto>>(
                Error.Forbidden(
                    "ModerationAudit.AdminRequired",
                    "Solo un administrador puede consultar la auditoría de moderación."));
        }

        ModerationTargetType? targetType = Enum.TryParse(request.TargetType, ignoreCase: true, out ModerationTargetType parsedTarget)
            ? parsedTarget
            : null;

        ModerationVerdict? verdict = Enum.TryParse(request.Verdict, ignoreCase: true, out ModerationVerdict parsedVerdict)
            ? parsedVerdict
            : null;

        (IReadOnlyList<ModerationAuditRecord> items, long total) = await _repository.ListAsync(
            request.TargetId,
            targetType,
            verdict,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize,
            cancellationToken);

        var page = new PagedResult<ModerationAuditRecordDto>(
            items.Select(record => record.ToDto()).ToList(),
            (int)Math.Min(total, int.MaxValue),
            request.Page,
            request.PageSize);

        return Result.Success(page);
    }
}
