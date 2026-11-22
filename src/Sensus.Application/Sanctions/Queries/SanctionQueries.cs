using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Sanctions.Queries;

/// <summary>FR-020-05: listado paginado de sanciones (20 por defecto, 100 máximo).</summary>
public sealed record ListSanctionsQuery(Guid? UserId, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SanctionDto>>>;

public sealed record GetSanctionQuery(Guid SanctionId) : IRequest<Result<SanctionDto>>;

public sealed class ListSanctionsQueryValidator : AbstractValidator<ListSanctionsQuery>
{
    public const int MaxPageSize = 100;

    public ListSanctionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.Status)
            .Must(status => Enum.TryParse<SanctionState>(status, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("El estado debe ser Active, Expired o Reverted.");
    }
}

public sealed class ListSanctionsQueryHandler
    : IRequestHandler<ListSanctionsQuery, Result<PagedResult<SanctionDto>>>
{
    private readonly ISanctionRepository _sanctions;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ListSanctionsQueryHandler(
        ISanctionRepository sanctions,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _sanctions = sanctions;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PagedResult<SanctionDto>>> Handle(
        ListSanctionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdministrator())
        {
            return Result.Failure<PagedResult<SanctionDto>>(
                Error.Forbidden("Sanction.AdminRequired", "Solo un administrador puede consultar sanciones."));
        }

        SanctionState? state = Enum.TryParse(request.Status, ignoreCase: true, out SanctionState parsed)
            ? parsed
            : null;

        DateTime now = _clock.UtcNow;
        (IReadOnlyList<Sanction> items, long total) = await _sanctions.ListAsync(
            request.UserId,
            state,
            now,
            request.Page,
            request.PageSize,
            cancellationToken);

        var page = new PagedResult<SanctionDto>(
            items.Select(s => s.ToDto(now)).ToList(),
            (int)Math.Min(total, int.MaxValue),
            request.Page,
            request.PageSize);

        return Result.Success(page);
    }
}

public sealed class GetSanctionQueryHandler : IRequestHandler<GetSanctionQuery, Result<SanctionDto>>
{
    private readonly ISanctionRepository _sanctions;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public GetSanctionQueryHandler(
        ISanctionRepository sanctions,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _sanctions = sanctions;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<SanctionDto>> Handle(GetSanctionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdministrator())
        {
            return Result.Failure<SanctionDto>(
                Error.Forbidden("Sanction.AdminRequired", "Solo un administrador puede consultar sanciones."));
        }

        Sanction? sanction = await _sanctions.GetByIdAsync(request.SanctionId, cancellationToken);
        return sanction is null
            ? Result.Failure<SanctionDto>(Error.NotFound("Sanction.NotFound", "La sanción indicada no existe."))
            : Result.Success(sanction.ToDto(_clock.UtcNow));
    }
}
