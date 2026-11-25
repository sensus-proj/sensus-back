using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de publicaciones del feed para pruebas sin MongoDB.</summary>
public sealed class InMemoryFeedPostRepository : IFeedPostRepository
{
    private readonly List<FeedPost> _posts = [];

    public Task AddAsync(FeedPost post, CancellationToken cancellationToken = default)
    {
        _posts.Add(post);
        return Task.CompletedTask;
    }

    public Task<FeedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));

    public Task UpdateAsync(FeedPost post, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<FeedPost>> GetPublishedPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FeedPost> page = _posts
            .Where(p => p.Status == FeedPostStatus.Published && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.Count(p => p.Status == FeedPostStatus.Published && !p.IsDeleted));

    public Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        int changed = _posts.Count(p => p.AuthorUserId == authorUserId && p.Anonymize(updatedAtUtc));
        return Task.FromResult(changed);
    }
}
