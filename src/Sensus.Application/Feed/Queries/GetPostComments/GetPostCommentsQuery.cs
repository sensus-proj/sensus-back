using FluentValidation;
using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Queries.GetPostComments;

public sealed record GetPostCommentsQuery(
    Guid PostId,
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize) : IRequest<Result<PagedResult<FeedCommentDto>>>;

public sealed class GetPostCommentsQueryValidator : AbstractValidator<GetPostCommentsQuery>
{
    public GetPostCommentsQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty()
            .WithMessage("El identificador de la publicación es obligatorio.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser al menos 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationPolicy.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationPolicy.MaxPageSize}.");
    }
}

public sealed class GetPostCommentsQueryHandler : IRequestHandler<GetPostCommentsQuery, Result<PagedResult<FeedCommentDto>>>
{
    private readonly IFeedCommentRepository _commentRepository;
    private readonly IFeedPostVisibilityLookup _postLookup;

    public GetPostCommentsQueryHandler(
        IFeedCommentRepository commentRepository,
        IFeedPostVisibilityLookup postLookup)
    {
        _commentRepository = commentRepository;
        _postLookup = postLookup;
    }

    public async Task<Result<PagedResult<FeedCommentDto>>> Handle(
        GetPostCommentsQuery request,
        CancellationToken cancellationToken)
    {
        FeedPostLookupResult postLookup = await _postLookup.GetVisibilityAsync(request.PostId, cancellationToken);
        if (!postLookup.Exists || !postLookup.IsVisible)
        {
            return Result.Failure<PagedResult<FeedCommentDto>>(
                Error.NotFound("FeedComment.PostNotFound", "La publicación no existe o no está disponible."));
        }

        IReadOnlyList<FeedComment> comments = await _commentRepository.GetByPostIdPagedAsync(
            request.PostId,
            request.Page,
            request.PageSize,
            cancellationToken);

        int totalCount = await _commentRepository.CountByPostIdAsync(request.PostId, cancellationToken);
        List<FeedCommentDto> items = comments.Select(c => c.ToDto()).ToList();

        return Result.Success(new PagedResult<FeedCommentDto>(items, totalCount, request.Page, request.PageSize));
    }
}
