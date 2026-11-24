using FluentAssertions;
using NSubstitute;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Comercios.Queries.GetStoreReviews;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Common;

public sealed class PaginationPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-3, 500, 1, 100)]
    [InlineData(2, 50, 2, 50)]
    public void Normalize_ClampsInvalidOrOversizedValues(int? page, int? size, int expectedPage, int expectedSize)
    {
        (int p, int s) = PaginationPolicy.Normalize(page, size);
        p.Should().Be(expectedPage);
        s.Should().Be(expectedSize);
    }

    [Fact]
    public async Task GetStoreReviewsHandler_PaginatesReviewsWhilePreservingGlobalSummary()
    {
        var comercioRepo = Substitute.For<IComercioRepository>();
        var reviewRepo = Substitute.For<IStoreReviewRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();

        Guid merchantId = Guid.NewGuid();
        Comercio comercio = Comercio.Reconstitute(
            merchantId,
            Guid.NewGuid(),
            "Panadería Centro",
            "Calle 5",
            "06000",
            19.43,
            -99.13,
            activo: true,
            Now,
            MerchantCategory.Cafeterias);

        List<StoreReview> reviews = Enumerable.Range(1, 5)
            .Select(i => StoreReview.Create(
                merchantId,
                Guid.NewGuid(),
                $"Cliente {i}",
                null,
                i % 2 == 0 ? 4 : 5,
                $"Reseña #{i}",
                Now.AddMinutes(i)).Value)
            .ToList();

        comercioRepo.GetByIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns(comercio);
        reviewRepo.GetByMerchantIdAsync(merchantId, Arg.Any<CancellationToken>()).Returns(reviews);

        var handler = new GetStoreReviewsQueryHandler(comercioRepo, reviewRepo, currentUser);
        Result<StoreReviewsSummaryDto> page2 = await handler.Handle(new GetStoreReviewsQuery(merchantId, Page: 2, PageSize: 2), CancellationToken.None);

        page2.IsSuccess.Should().BeTrue();
        page2.Value.TotalReviews.Should().Be(5);
        page2.Value.PageNumber.Should().Be(2);
        page2.Value.PageSize.Should().Be(2);
        page2.Value.TotalPages.Should().Be(3);
        page2.Value.HasPreviousPage.Should().BeTrue();
        page2.Value.HasNextPage.Should().BeTrue();
        page2.Value.Reviews.Should().HaveCount(2);
        page2.Value.Reviews.Select(r => r.Comment).Should().Equal("Reseña #3", "Reseña #2");
    }

    [Fact]
    public async Task GetStoreReviewsQueryValidator_RejectsOutOfRangePagination()
    {
        var validator = new GetStoreReviewsQueryValidator();
        Guid merchantId = Guid.NewGuid();

        (await validator.ValidateAsync(new GetStoreReviewsQuery(merchantId, Page: 0, PageSize: 20))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetStoreReviewsQuery(merchantId, Page: 1, PageSize: 101))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetStoreReviewsQuery(merchantId, Page: 1, PageSize: 100))).IsValid.Should().BeTrue();
    }
}
