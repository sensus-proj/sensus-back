using FluentAssertions;
using MongoDB.Bson;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;

namespace Sensus.IntegrationTests.Persistence;

public sealed class MongoFeedPostVisibilityLookupTests
{
    [Theory]
    [InlineData("Published", false, true)]
    [InlineData("PendingModeration", false, false)]
    [InlineData("Quarantined", false, false)]
    [InlineData("Deleted", false, false)]
    [InlineData("Published", true, false)]
    [InlineData(null, false, false)]
    public void IsVisible_AllowsOnlyPublishedAndNotDeletedPosts(string? status, bool isDeleted, bool expected)
    {
        var doc = new BsonDocument
        {
            ["isDeleted"] = isDeleted
        };

        if (status is not null)
        {
            doc["status"] = status;
        }

        MongoFeedPostVisibilityLookup.IsVisible(doc).Should().Be(expected);
    }

    [Fact]
    public void IsVisible_UsesPendingModerationAsDefaultStatus()
    {
        var doc = new BsonDocument
        {
            ["isDeleted"] = false
        };

        MongoFeedPostVisibilityLookup.IsVisible(doc).Should().BeFalse();
        FeedPostStatus.PendingModeration.ToString().Should().Be("PendingModeration");
    }
}
