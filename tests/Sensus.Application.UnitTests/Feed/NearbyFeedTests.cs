using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed;
using Sensus.Application.Feed.Commands.CreateFeedPost;
using Sensus.Application.Feed.Commands.DeleteFeedPost;
using Sensus.Application.Feed.DTOs;
using Sensus.Application.Feed.Queries.GetNearbyFeed;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Feed;

/// <summary>
/// Spec 026: feed geolocalizado (FR-026-01 a FR-026-06, CA-026-01 a CA-026-07).
/// </summary>
public sealed class NearbyFeedTests
{
    private const double Lat = -12.05;
    private const double Lng = -77.04;
    private static readonly DateTime Now = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    // ---------- Dominio: ubicación de la publicación ----------

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(-12.05, -77.04, true)]
    [InlineData(90d, 180d, true)]
    [InlineData(-12.05, null, false)]
    [InlineData(null, -77.04, false)]
    [InlineData(90.1, 0d, false)]
    [InlineData(0d, -180.1, false)]
    [InlineData(double.NaN, 0d, false)]
    [InlineData(0d, double.PositiveInfinity, false)]
    public void ValidateLocation_RequiresBothFiniteCoordinatesInRange(double? lat, double? lng, bool expected)
    {
        FeedPost.ValidateLocation(lat, lng).IsSuccess.Should().Be(expected);
    }

    [Fact]
    public void Create_StoresLocation_AndRejectsIncompleteLocation()
    {
        FeedPost withLocation = FeedPost.Create(Guid.NewGuid(), "Ana", "Hola", null, Now, latitude: Lat, longitude: Lng).Value;
        withLocation.HasLocation.Should().BeTrue();
        withLocation.Latitude.Should().Be(Lat);
        withLocation.Longitude.Should().Be(Lng);

        FeedPost withoutLocation = FeedPost.Create(Guid.NewGuid(), "Ana", "Hola", null, Now).Value;
        withoutLocation.HasLocation.Should().BeFalse();

        Result<FeedPost> incomplete = FeedPost.Create(Guid.NewGuid(), "Ana", "Hola", null, Now, latitude: Lat);
        incomplete.Error.Code.Should().Be("FeedPost.IncompleteLocation");

        FeedPost rehydrated = FeedPost.Rehydrate(
            withLocation.Id, withLocation.AuthorUserId, "Ana", null, "Hola", null,
            withLocation.Status, false, Now, null, null, latitude: Lat, longitude: Lng);
        rehydrated.HasLocation.Should().BeTrue();
    }

    // ---------- Validación de borde ----------

    [Fact]
    public void Validator_AcceptsRadiusUpTo5KmInclusive_AndRejects501()
    {
        var validator = new GetNearbyFeedQueryValidator();

        validator.Validate(new GetNearbyFeedQuery(Lat, Lng, 5)).IsValid.Should().BeTrue();
        validator.Validate(new GetNearbyFeedQuery(Lat, Lng, 0.5)).IsValid.Should().BeTrue();

        // CA-026-05
        validator.Validate(new GetNearbyFeedQuery(Lat, Lng, 5.01)).IsValid.Should().BeFalse();
        validator.Validate(new GetNearbyFeedQuery(Lat, Lng, 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetNearbyFeedQuery(Lat, Lng, double.NaN)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(90.5, 0d)]
    [InlineData(-91d, 0d)]
    [InlineData(0d, 181d)]
    [InlineData(double.NaN, 0d)]
    [InlineData(0d, double.NegativeInfinity)]
    public void Validator_RejectsInvalidCoordinates(double lat, double lng)
    {
        new GetNearbyFeedQueryValidator().Validate(new GetNearbyFeedQuery(lat, lng)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validator_RejectsInvalidPagination(int page, int pageSize)
    {
        new GetNearbyFeedQueryValidator()
            .Validate(new GetNearbyFeedQuery(Lat, Lng, 5, page, pageSize))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Query_DefaultsToMaxRadiusAndStandardPagination()
    {
        var query = new GetNearbyFeedQuery(Lat, Lng);

        query.RadiusKm.Should().Be(NearbyFeedPolicy.MaxRadiusKm);
        query.Page.Should().Be(PaginationPolicy.DefaultPageNumber);
        query.PageSize.Should().Be(PaginationPolicy.DefaultPageSize);
    }

    // ---------- Handler: proyección y paginación ----------

    [Fact]
    public async Task Handler_MapsRepositoryPageAndPaginationMetadata()
    {
        // El filtrado, el orden y el desempate reales se prueban contra MongoDB (MongoNearbyFeedIntegrationTests).
        var repo = Substitute.For<IFeedPostRepository>();
        FeedPost newer = FeedPost.Create(Guid.NewGuid(), "Ana", "Reciente", null, Now, latitude: Lat, longitude: Lng).Value;
        FeedPost older = FeedPost.Create(Guid.NewGuid(), "Luis", "Antigua", null, Now.AddHours(-1), latitude: Lat, longitude: Lng).Value;
        repo.GetPublishedNearbyPagedAsync(Lat, Lng, 5000d, 1, 2, Arg.Any<CancellationToken>())
            .Returns((new List<FeedPost> { newer, older }, 5));

        var handler = new GetNearbyFeedQueryHandler(repo, NullLogger<GetNearbyFeedQueryHandler>.Instance);

        Result<PagedResult<NearbyFeedPostDto>> result = await handler.Handle(
            new GetNearbyFeedQuery(Lat, Lng, 5, 1, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
        result.Value.Items[0].Content.Should().Be("Reciente");
        result.Value.Items[0].AuthorDisplayName.Should().Be("Ana");
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
    }

    // ---------- Handler: caché (FR-026-06, CA-026-04) ----------

    [Fact]
    public async Task Handler_StoresMissInCache_AndServesHitWithoutQueryingDatabase()
    {
        var repo = Substitute.For<IFeedPostRepository>();
        var cache = Substitute.For<ICacheService>();
        FeedPost post = FeedPost.Create(Guid.NewGuid(), "Ana", "Hola", null, Now, latitude: Lat, longitude: Lng).Value;

        repo.GetPublishedNearbyPagedAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<FeedPost> { post }, 1));
        cache.GetAsync<NearbyFeedCacheEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NearbyFeedCacheEntry?)null);

        var handler = new GetNearbyFeedQueryHandler(repo, NullLogger<GetNearbyFeedQueryHandler>.Instance, cache);
        string expectedKey = FeedCacheKeys.Nearby(Lat, Lng, 5, 1, 20);

        Result<PagedResult<NearbyFeedPostDto>> miss = await handler.Handle(new GetNearbyFeedQuery(Lat, Lng, 5), CancellationToken.None);

        miss.Value.Items.Should().ContainSingle(i => i.Id == post.Id);
        await repo.Received(1).GetPublishedNearbyPagedAsync(Lat, Lng, 5000d, 1, 20, Arg.Any<CancellationToken>());
        await cache.Received(1).SetForEntityAsync(
            expectedKey,
            Arg.Is<NearbyFeedCacheEntry>(e => e.TotalCount == 1 && e.Items.Count == 1),
            FeedCacheKeys.NearbyCacheEntity,
            Arg.Any<CancellationToken>());

        // Segunda consulta idéntica → caché.
        repo.ClearReceivedCalls();
        cache.GetAsync<NearbyFeedCacheEntry>(expectedKey, Arg.Any<CancellationToken>())
            .Returns(new NearbyFeedCacheEntry([post.ToNearbyDto()], 1));

        Result<PagedResult<NearbyFeedPostDto>> hit = await handler.Handle(new GetNearbyFeedQuery(Lat, Lng, 5), CancellationToken.None);

        hit.Value.TotalCount.Should().Be(1);
        hit.Value.Items.Should().ContainSingle(i => i.Id == post.Id);
        await repo.DidNotReceive().GetPublishedNearbyPagedAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_RoundsCoordinatesForQueryAndCacheKey()
    {
        var repo = Substitute.For<IFeedPostRepository>();
        repo.GetPublishedNearbyPagedAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<FeedPost>(), 0));
        var handler = new GetNearbyFeedQueryHandler(repo, NullLogger<GetNearbyFeedQueryHandler>.Instance);

        await handler.Handle(new GetNearbyFeedQuery(-12.050049, -77.040051, 2.5), CancellationToken.None);

        await repo.Received(1).GetPublishedNearbyPagedAsync(-12.05, -77.0401, 2500d, 1, 20, Arg.Any<CancellationToken>());
        FeedCacheKeys.Nearby(-12.05, -77.0401, 2.5, 1, 20).Should().Be("feed:nearby:-12.0500:-77.0401:2.50:1:20");
    }

    [Fact]
    public void NearbyDto_DoesNotExposeCoordinatesOrStatus()
    {
        typeof(NearbyFeedPostDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(["Latitude", "Longitude", "Location", "Status", "IsDeleted"]);
    }

    // ---------- Invalidación (CA-026-06) ----------

    [Fact]
    public async Task DeletePost_InvalidatesNearbyCache()
    {
        var repo = Substitute.For<IFeedPostRepository>();
        var cache = Substitute.For<ICacheService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        Guid authorId = Guid.NewGuid();
        FeedPost post = FeedPost.Create(authorId, "Ana", "Hola", null, Now, latitude: Lat, longitude: Lng).Value;

        currentUser.UserId.Returns(authorId.ToString());
        currentUser.Roles.Returns(new List<string>());
        clock.UtcNow.Returns(Now);
        repo.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var handler = new DeleteFeedPostCommandHandler(
            repo, currentUser, clock, NullLogger<DeleteFeedPostCommandHandler>.Instance, cacheService: cache);

        (await handler.Handle(new DeleteFeedPostCommand(post.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();

        await cache.Received(1).RemoveByPrefixAsync(FeedCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePost_DoesNotInvalidate_WhenForbidden()
    {
        var repo = Substitute.For<IFeedPostRepository>();
        var cache = Substitute.For<ICacheService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        FeedPost post = FeedPost.Create(Guid.NewGuid(), "Ana", "Hola", null, Now, latitude: Lat, longitude: Lng).Value;

        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        currentUser.Roles.Returns(new List<string>());
        repo.GetByIdAsync(post.Id, Arg.Any<CancellationToken>()).Returns(post);

        var handler = new DeleteFeedPostCommandHandler(
            repo, currentUser, Substitute.For<IDateTimeProvider>(), NullLogger<DeleteFeedPostCommandHandler>.Instance, cacheService: cache);

        (await handler.Handle(new DeleteFeedPostCommand(post.Id), CancellationToken.None)).IsFailure.Should().BeTrue();

        await cache.DidNotReceive().RemoveByPrefixAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreatePost_PersistsLocation_AndInvalidatesCacheOnlyWhenLocated(bool withLocation)
    {
        var repo = Substitute.For<IFeedPostRepository>();
        var cache = Substitute.For<ICacheService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var moderation = Substitute.For<ITextModerationService>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        clock.UtcNow.Returns(Now);
        moderation.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(IsApproved: true));

        var handler = new CreateFeedPostCommandHandler(
            repo,
            Substitute.For<IUserRepository>(),
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: moderation,
            cacheService: cache,
            auditRecorder: auditRecorder);

        var command = withLocation
            ? new CreateFeedPostCommand("Hola barrio", Latitude: Lat, Longitude: Lng)
            : new CreateFeedPostCommand("Hola barrio");

        (await handler.Handle(command, CancellationToken.None)).IsSuccess.Should().BeTrue();

        await repo.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.HasLocation == withLocation),
            Arg.Any<CancellationToken>());
        await cache.Received(withLocation ? 1 : 0)
            .RemoveByPrefixAsync(FeedCacheKeys.NearbyPrefix, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreateValidator_RejectsIncompleteOrOutOfRangeLocation()
    {
        var validator = new CreateFeedPostCommandValidator();

        validator.Validate(new CreateFeedPostCommand("Hola", Latitude: Lat, Longitude: Lng)).IsValid.Should().BeTrue();
        validator.Validate(new CreateFeedPostCommand("Hola", Latitude: Lat)).IsValid.Should().BeFalse();
        validator.Validate(new CreateFeedPostCommand("Hola", Latitude: 95, Longitude: Lng)).IsValid.Should().BeFalse();
    }
}
