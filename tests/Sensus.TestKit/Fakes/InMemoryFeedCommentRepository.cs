using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryFeedCommentRepository : IFeedCommentRepository
{
    private readonly List<FeedComment> _comments = [];

    public IReadOnlyList<FeedComment> All => _comments;

    public Task AddAsync(FeedComment comment, CancellationToken cancellationToken = default)
    {
        _comments.Add(comment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeedComment>> GetByPostIdPagedAsync(
        Guid postId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FeedComment> page = _comments
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAtUtc)
            .Skip((Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();

        return Task.FromResult(page);
    }

    public Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_comments.Count(c => c.PostId == postId));
}
