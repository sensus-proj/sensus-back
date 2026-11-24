using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Sensus.Application.Comercios;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Comercios.Queries.GetStoreReviews;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Comercios.Queries;

public sealed class GetStoreReviewsQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly FakeStoreReviewRepository _storeReviewRepository = new();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly GetStoreReviewsQueryHandler _handler;
    private readonly GetStoreReviewsQueryValidator _validator = new();

    public GetStoreReviewsQueryHandlerTests()
    {
        _currentUserService.IsGuest.Returns(false);
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _handler = new GetStoreReviewsQueryHandler(
            _comercioRepository,
            _storeReviewRepository,
            _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenComercioNotFound_ReturnsNotFoundError()
    {
        var merchantId = Guid.NewGuid();
        _comercioRepository.GetByIdAsync(merchantId, Arg.Any<CancellationToken>())
            .Returns((Comercio?)null);

        var query = new GetStoreReviewsQuery(merchantId, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.NotFound");
    }

    [Fact]
    public async Task Handle_WhenComercioInactive_ReturnsNotFoundError()
    {
        var comercio = CreateComercio(activo: false);
        _comercioRepository.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(comercio);

        var query = new GetStoreReviewsQuery(comercio.Id, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.NotFound");
    }

    [Fact]
    public async Task Handle_WhenComercioHasNoReviews_ReturnsEmptySummaryWithZeroAverage()
    {
        var comercio = CreateComercio(activo: true);
        _comercioRepository.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(comercio);

        var query = new GetStoreReviewsQuery(comercio.Id, 1, 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        StoreReviewsSummaryDto summary = result.Value;
        summary.MerchantId.Should().Be(comercio.Id);
        summary.AverageRating.Should().Be(0.0);
        summary.TotalReviews.Should().Be(0);
        summary.Reviews.Should().BeEmpty();
        summary.Breakdown.Should().HaveCount(5);
        summary.Breakdown.Should().OnlyContain(b => b.Count == 0);
        summary.TotalPages.Should().Be(1);
        summary.HasNextPage.Should().BeFalse();
        summary.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithMultiplePages_ReturnsPagedReviewsAndCorrectPaginationFlags()
    {
        var comercio = CreateComercio(activo: true);
        _comercioRepository.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(comercio);

        // Seed 5 reviews: 3 with rating 5, 1 with 4, 1 with 3
        for (int i = 0; i < 5; i++)
        {
            int rating = i switch
            {
                < 3 => 5,
                3 => 4,
                _ => 3
            };
            var review = StoreReview.Create(
                comercio.Id,
                Guid.NewGuid(),
                $"Usuario {i}",
                null,
                rating,
                $"Comentario número {i}",
                FixedNow.AddMinutes(i)).Value;

            await _storeReviewRepository.AddAsync(review);
        }

        // Page 1 with pageSize 2 (debería retornar 2 items, total 5, 3 páginas, hasNext=true, hasPrev=false)
        var page1 = (await _handler.Handle(new GetStoreReviewsQuery(comercio.Id, 1, 2), CancellationToken.None)).Value;
        page1.Reviews.Should().HaveCount(2);
        page1.TotalReviews.Should().Be(5);
        page1.PageNumber.Should().Be(1);
        page1.PageSize.Should().Be(2);
        page1.TotalPages.Should().Be(3);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();
        page1.AverageRating.Should().Be(4.4); // (5+5+5+4+3)/5 = 22/5 = 4.4

        // Page 2 with pageSize 2 (debería retornar 2 items, hasNext=true, hasPrev=true)
        var page2 = (await _handler.Handle(new GetStoreReviewsQuery(comercio.Id, 2, 2), CancellationToken.None)).Value;
        page2.Reviews.Should().HaveCount(2);
        page2.HasNextPage.Should().BeTrue();
        page2.HasPreviousPage.Should().BeTrue();

        // Page 3 with pageSize 2 (debería retornar 1 item, hasNext=false, hasPrev=true)
        var page3 = (await _handler.Handle(new GetStoreReviewsQuery(comercio.Id, 3, 2), CancellationToken.None)).Value;
        page3.Reviews.Should().HaveCount(1);
        page3.HasNextPage.Should().BeFalse();
        page3.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void BuildSummary_EquivalenceTest_MongoStatsAndInMemoryProduceExactSameSummary()
    {
        var comercio = CreateComercio(activo: true);
        var reviews = new List<StoreReview>();
        int[] ratings = [5, 5, 4, 4, 3, 2, 1, 5, 4, 3];

        for (int i = 0; i < ratings.Length; i++)
        {
            reviews.Add(StoreReview.Create(
                comercio.Id,
                Guid.NewGuid(),
                $"Usuario {i}",
                null,
                ratings[i],
                $"Comentario de prueba {i}",
                FixedNow.AddMinutes(i)).Value);
        }

        // Simular stats de Mongo
        var countsByStar = reviews.GroupBy(r => r.Rating).ToDictionary(g => g.Key, g => g.Count());
        double avg = Math.Round(reviews.Average(r => r.Rating), 1);
        var stats = new StoreReviewRatingStats(avg, reviews.Count, countsByStar);

        int page = 1;
        int pageSize = 4;
        var pagedSlice = reviews.OrderByDescending(r => r.CreatedAtUtc).Take(pageSize).ToList();

        StoreReviewsSummaryDto fromStats = StoreReviewMappingExtensions.BuildSummary(
            comercio,
            stats,
            pagedSlice,
            reviews.Count,
            null,
            page,
            pageSize);

        StoreReviewsSummaryDto inMemory = StoreReviewMappingExtensions.BuildSummary(
            comercio,
            reviews,
            null,
            page,
            pageSize);

        fromStats.AverageRating.Should().Be(inMemory.AverageRating);
        fromStats.TotalReviews.Should().Be(inMemory.TotalReviews);
        fromStats.Breakdown.Should().BeEquivalentTo(inMemory.Breakdown, options => options.WithStrictOrdering());
        fromStats.Reviews.Should().BeEquivalentTo(inMemory.Reviews, options => options.WithStrictOrdering());
        fromStats.TotalPages.Should().Be(inMemory.TotalPages);
        fromStats.HasNextPage.Should().Be(inMemory.HasNextPage);
        fromStats.HasPreviousPage.Should().Be(inMemory.HasPreviousPage);
    }

    [Fact]
    public void Validator_WhenPageSizeExceeds100_FailsValidation()
    {
        var query = new GetStoreReviewsQuery(Guid.NewGuid(), 1, 101);
        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage("El tamaño de página debe estar entre 1 y 100.");
    }

    [Fact]
    public void Validator_WhenPageIsZeroOrNegative_FailsValidation()
    {
        var query = new GetStoreReviewsQuery(Guid.NewGuid(), 0, 20);
        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Page)
            .WithErrorMessage("El número de página debe ser al menos 1.");
    }

    private static Comercio CreateComercio(bool activo)
    {
        var result = Comercio.Create(
            Guid.NewGuid(),
            "Café Central",
            "Av. Reforma 123",
            "03940",
            19.4326,
            -99.1332);

        Comercio comercio = result.Value;
        if (!activo)
        {
            comercio.Deactivate();
        }

        return comercio;
    }

    private sealed class FakeStoreReviewRepository : IStoreReviewRepository
    {
        private readonly List<StoreReview> _reviews = [];

        public Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_reviews.FirstOrDefault(r => r.Id == reviewId));

        public Task<IReadOnlyList<StoreReview>> GetByMerchantIdAsync(
            Guid merchantId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<StoreReview> items = _reviews
                .Where(r => r.MerchantId == merchantId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
            return Task.FromResult(items);
        }

        public Task<(IReadOnlyList<StoreReview> Items, long TotalCount)> GetPagedByMerchantIdAsync(
            Guid merchantId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var matching = _reviews
                .Where(r => r.MerchantId == merchantId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();

            int safePage = Math.Max(1, pageNumber);
            int safePageSize = Math.Max(1, pageSize);
            int skip = (safePage - 1) * safePageSize;

            IReadOnlyList<StoreReview> items = matching
                .Skip(skip)
                .Take(safePageSize)
                .ToList();

            return Task.FromResult((items, (long)matching.Count));
        }

        public Task<StoreReviewRatingStats> GetRatingSummaryAsync(
            Guid merchantId,
            CancellationToken cancellationToken = default)
        {
            var matching = _reviews.Where(r => r.MerchantId == merchantId).ToList();
            int total = matching.Count;
            double avg = total == 0 ? 0.0 : Math.Round(matching.Average(r => r.Rating), 1);

            var countsByStar = matching
                .GroupBy(r => r.Rating)
                .ToDictionary(g => g.Key, g => g.Count());

            return Task.FromResult(new StoreReviewRatingStats(avg, total, countsByStar));
        }

        public Task<Dictionary<Guid, (double AverageRating, int ReviewCount)>> GetRatingSummariesByMerchantIdsAsync(
            IEnumerable<Guid> merchantIds,
            CancellationToken cancellationToken = default)
        {
            HashSet<Guid> targetIds = merchantIds.ToHashSet();
            var summaries = _reviews
                .Where(r => targetIds.Contains(r.MerchantId))
                .GroupBy(r => r.MerchantId)
                .ToDictionary(
                    g => g.Key,
                    g => (Math.Round(g.Average(r => r.Rating), 1), g.Count()));
            return Task.FromResult(summaries);
        }

        public Task<IReadOnlyList<StoreReview>> GetRecentActivityByUserIdsAsync(
            IEnumerable<Guid> userIds,
            int limit,
            CancellationToken cancellationToken = default)
        {
            HashSet<Guid> targetUsers = userIds.ToHashSet();
            IReadOnlyList<StoreReview> items = _reviews
                .Where(r => targetUsers.Contains(r.AuthorUserId)
                            || r.Replies.Any(reply => targetUsers.Contains(reply.AuthorUserId)))
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(Math.Max(0, limit))
                .ToList();
            return Task.FromResult(items);
        }

        public Task AddAsync(StoreReview review, CancellationToken cancellationToken = default)
        {
            _reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(StoreReview review, CancellationToken cancellationToken = default)
        {
            int index = _reviews.FindIndex(r => r.Id == review.Id);
            if (index >= 0)
            {
                _reviews[index] = review;
            }

            return Task.CompletedTask;
        }
    }
}
