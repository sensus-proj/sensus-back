using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Infrastructure;

public sealed class MongoCommunityActivityTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7.0").Build();
    private MongoDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _mongo.GetConnectionString() }),
            NullLogger<MongoDbContext>.Instance);
    }

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();

    [Fact]
    public async Task ImageProof_RequiresOwnerApprovedStateAndExactText()
    {
        var owner = Guid.NewGuid();
        const string url = "https://example.test/media/approved";
        var repository = new MongoMediaAssetRepository(_context, NullLogger<MongoMediaAssetRepository>.Instance);
        var asset = MediaAsset.Create(owner, "approved", url, "image/jpeg", MediaAssetStatus.Approved,
            DateTime.UtcNow, "fingerprint");
        await repository.AddAsync(asset);

        Assert.True(await repository.HasApprovedImageAsync(owner, url, "fingerprint"));
        Assert.False(await repository.HasApprovedImageAsync(Guid.NewGuid(), url, "fingerprint"));
        Assert.False(await repository.HasApprovedImageAsync(owner, url, "different"));

        var loaded = await repository.GetByIdAsync(asset.Id);
        Assert.NotNull(loaded);
        loaded.Quarantine();
        Assert.True(loaded.Release());
        await repository.UpdateAsync(loaded);
        Assert.False(await repository.HasApprovedImageAsync(owner, url, "fingerprint"));
    }

    [Fact]
    public async Task Activity_PagesVisibleEventsAcrossOffersReviewsAndReplies()
    {
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var viewer = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var merchant = Comercio.Create(Guid.NewGuid(), "Comercio de prueba", "Calle 1", "03100", 19.43, -99.13).Value;
        var merchants = new MongoComercioRepository(_context, NullLogger<MongoComercioRepository>.Instance);
        var offers = new MongoOfferRepository(_context, NullLogger<MongoOfferRepository>.Instance);
        var likes = new MongoOfferLikeRepository(_context);
        var reviews = new MongoStoreReviewRepository(_context);
        var reader = new MongoCommunityActivityReader(_context);
        await merchants.AddAsync(merchant);
        var offer = Offer.Create(merchant.Id, "Oferta pública", "Oferta disponible para clientes",
            100, 10, 2, now, now.AddDays(1), nowUtc: now).Value;
        var draft = Offer.Create(merchant.Id, "Borrador privado", "Contenido que no debe aparecer",
            100, 10, 2, now, now.AddDays(1), nowUtc: now, saveAsDraft: true).Value;
        await offers.AddAsync(offer);
        await offers.AddAsync(draft);
        await likes.ToggleLikeAsync(friend, offer.Id, now.AddMinutes(1));
        await likes.ToggleLikeAsync(stranger, offer.Id, now.AddMinutes(10));
        await likes.ToggleLikeAsync(viewer, draft.Id, now.AddMinutes(11));

        var ownReview = StoreReview.Create(merchant.Id, viewer, "Comprador", null, 5, "Mi reseña", now).Value;
        await reviews.AddAsync(ownReview);
        var otherReview = StoreReview.Create(merchant.Id, stranger, "Tercero", null, 4, "Reseña ajena", now.AddDays(-1)).Value;
        await reviews.AddAsync(otherReview);
        var reply = StoreReviewReply.Create(friend, "Amiga", null, "Respuesta visible", now.AddMinutes(2)).Value;
        Assert.True(await reviews.AddReplyAsync(otherReview.Id, reply));
        var unrelatedReply = StoreReviewReply.Create(stranger, "Tercero", null, "Respuesta ajena", now.AddMinutes(15)).Value;
        Assert.True(await reviews.AddReplyAsync(otherReview.Id, unrelatedReply));

        var first = await reader.ReadAsync([viewer, friend], 1, 1, CancellationToken.None);
        var second = await reader.ReadAsync([viewer, friend], 2, 1, CancellationToken.None);
        var third = await reader.ReadAsync([viewer, friend], 3, 1, CancellationToken.None);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal("Respuesta visible", Assert.Single(first.Items).CommentText);
        Assert.Equal("OfferLike", Assert.Single(second.Items).ActivityType);
        Assert.Equal("Mi reseña", Assert.Single(third.Items).CommentText);
        Assert.False(third.HasNextPage);
        var changed = await reviews.ToggleLikeAsync(ownReview.Id, friend);
        Assert.Equal((true, 1), changed);
        Assert.Equal((false, 0), await reviews.ToggleLikeAsync(ownReview.Id, friend));
        var (page, ratings) = await reviews.GetMerchantPageAsync(merchant.Id, 1, 1);
        Assert.Single(page);
        Assert.Equal(2, ratings.Values.Sum());
    }
}
