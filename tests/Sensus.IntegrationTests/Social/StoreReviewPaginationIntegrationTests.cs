using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Sensus.IntegrationTests.Infrastructure;
using Testcontainers.MongoDb;
using Xunit;

namespace Sensus.IntegrationTests.Social;

/// <summary>
/// Pruebas de integración para paginación y agregación en base de datos de StoreReview (Spec 056 / FR-056-07 / T056-05).
/// </summary>
public sealed class StoreReviewMongoPaginationIntegrationTests : IAsyncLifetime
{
    private static readonly DateTime FixedNow = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_reviews_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoStoreReviewRepository _repository = null!;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _connectionString = _envConn;
        }
        else
        {
            _container = new MongoDbBuilder("mongo:7.0").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);

        _repository = new MongoStoreReviewRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }
        else if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task EmptyMerchant_ReturnsZeroTotalAndEmptyBreakdown()
    {
        var merchantId = Guid.NewGuid();

        var stats = await _repository.GetRatingSummaryAsync(merchantId);
        var (pagedReviews, totalCount) = await _repository.GetPagedByMerchantIdAsync(merchantId, 1, 20);

        stats.TotalReviews.Should().Be(0);
        stats.AverageRating.Should().Be(0.0);
        stats.CountsByStar.Should().BeEmpty();

        totalCount.Should().Be(0);
        pagedReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleReviews_CalculatesMongoAggregationAndPagesCorrectly()
    {
        var merchantId = Guid.NewGuid();
        // 25 reseñas con distribución conocida:
        // 10 con 5 estrellas, 5 con 4, 5 con 3, 3 con 2, 2 con 1
        // Promedio esperado: (10*5 + 5*4 + 5*3 + 3*2 + 2*1) / 25 = (50 + 20 + 15 + 6 + 2) / 25 = 93 / 25 = 3.72 -> 3.7
        int[] starDistribution = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3, 2, 2, 2, 1, 1];

        for (int i = 0; i < starDistribution.Length; i++)
        {
            var review = StoreReview.Create(
                merchantId,
                Guid.NewGuid(),
                $"Cliente {i}",
                null,
                starDistribution[i],
                $"Comentario de prueba número {i}",
                FixedNow.AddMinutes(i)).Value;

            await _repository.AddAsync(review);
        }

        // 1. Verificar agregación $match + $group en MongoDB real
        StoreReviewRatingStats stats = await _repository.GetRatingSummaryAsync(merchantId);
        stats.TotalReviews.Should().Be(25);
        stats.AverageRating.Should().Be(3.7);
        stats.CountsByStar[5].Should().Be(10);
        stats.CountsByStar[4].Should().Be(5);
        stats.CountsByStar[3].Should().Be(5);
        stats.CountsByStar[2].Should().Be(3);
        stats.CountsByStar[1].Should().Be(2);

        // 2. Página 1 (pageSize = 10)
        var (page1Items, page1Total) = await _repository.GetPagedByMerchantIdAsync(merchantId, 1, 10);
        page1Total.Should().Be(25);
        page1Items.Should().HaveCount(10);
        // Debe estar ordenado descendentemente por CreatedAtUtc
        page1Items.Should().BeInDescendingOrder(r => r.CreatedAtUtc);
        page1Items[0].Comment.Should().Be("Comentario de prueba número 24");

        // 3. Página 2 (pageSize = 10)
        var (page2Items, page2Total) = await _repository.GetPagedByMerchantIdAsync(merchantId, 2, 10);
        page2Total.Should().Be(25);
        page2Items.Should().HaveCount(10);
        page2Items[0].Comment.Should().Be("Comentario de prueba número 14");

        // 4. Página 3 (pageSize = 10, última página)
        var (page3Items, page3Total) = await _repository.GetPagedByMerchantIdAsync(merchantId, 3, 10);
        page3Total.Should().Be(25);
        page3Items.Should().HaveCount(5);
        page3Items[^1].Comment.Should().Be("Comentario de prueba número 0");
    }
}

public sealed class StoreReviewHttpPaginationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StoreReviewHttpPaginationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        var tokenGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        string guestToken = tokenGen.GenerateGuestToken(Guid.NewGuid()).Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
    }

    [Fact]
    public async Task GetStoreReviews_WhenPageSizeExceeds100_Returns400BadRequest()
    {
        var merchantId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/v1/merchants/{merchantId}/reviews?page=1&pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().NotBeNullOrWhiteSpace();
        problem.Status.Should().Be(400);
    }

    [Fact]
    public async Task GetStoreReviews_WhenValid_Returns200WithPaginationMetadata()
    {
        var merchantId = Guid.NewGuid();
        var comercio = Comercio.Create(
            Guid.NewGuid(),
            "Café Test",
            "Reforma 100",
            "03940",
            19.4326,
            -99.1332).Value;

        _factory.ComercioRepository.GetByIdAsync(merchantId, Arg.Any<CancellationToken>())
            .Returns(comercio);

        _factory.StoreReviewRepository.GetRatingSummaryAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(new StoreReviewRatingStats(4.8, 10, new Dictionary<int, int> { [5] = 8, [4] = 2 }));

        _factory.StoreReviewRepository.GetPagedByMerchantIdAsync(comercio.Id, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<StoreReview>(), 10L));

        var response = await _client.GetAsync($"/api/v1/merchants/{merchantId}/reviews?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<StoreReviewsSummaryDto>();
        summary.Should().NotBeNull();
        summary!.AverageRating.Should().Be(4.8);
        summary.TotalReviews.Should().Be(10);
        summary.PageNumber.Should().Be(1);
        summary.PageSize.Should().Be(20);
    }
}
