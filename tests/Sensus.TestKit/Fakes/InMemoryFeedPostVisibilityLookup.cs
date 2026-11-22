using System.Collections.Concurrent;
using Sensus.Application.Common.Interfaces;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryFeedPostVisibilityLookup : IFeedPostVisibilityLookup
{
    private readonly ConcurrentDictionary<Guid, FeedPostLookupResult> _posts = new();

    public void RegisterPost(Guid postId, bool isVisible, Guid? authorUserId = null) =>
        _posts[postId] = new FeedPostLookupResult(true, isVisible, authorUserId);

    public Task<FeedPostLookupResult> GetVisibilityAsync(Guid postId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.TryGetValue(postId, out FeedPostLookupResult? result)
            ? result
            : new FeedPostLookupResult(false, false, null));
}
