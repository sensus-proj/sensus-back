using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Reports.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Reports;

public sealed class ContentReportEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ContentReportEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(new DateTime(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc));
        _factory.ContentReportRepository.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Carlos Vera", $"carlos.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        return (tokenGenerator.GenerateToken(user).Token, user);
    }

    [Fact]
    public async Task CreateReport_ReturnsCreatedAndTriggersReviewAtThreshold_WithoutExposingReporterPii()
    {
        (string token, _) = CreateRegisteredToken();
        Guid offerId = Guid.NewGuid();
        _factory.ReportTargetService.GetTargetInfoAsync(ReportTargetType.Offer, offerId, Arg.Any<CancellationToken>())
            .Returns(new ReportTargetInfo(true, Guid.NewGuid()));
        _factory.ContentReportRepository.ExistsRecentAsync(Arg.Any<Guid>(), ReportTargetType.Offer, offerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _factory.ContentReportRepository.TryAddAsync(Arg.Any<ContentReport>(), Arg.Any<CancellationToken>()).Returns(true);
        _factory.ContentReportRepository.CountRecentByTargetAsync(ReportTargetType.Offer, offerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);
        _factory.ReportTargetService.MarkUnderReviewAsync(ReportTargetType.Offer, offerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/reports",
            new { targetType = "offer", targetId = offerId, reason = "fraude", details = "El precio en tienda era el doble" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        string rawJson = await response.Content.ReadAsStringAsync();
        rawJson.ToLowerInvariant().Should().NotContain("reporteruserid");

        ContentReportDto? dto = await response.Content.ReadFromJsonAsync<ContentReportDto>();
        dto.Should().NotBeNull();
        dto!.TargetMarkedForReview.Should().BeTrue();
        dto.TargetType.Should().Be("offer");
    }

    [Fact]
    public async Task CreateReport_ReturnsBadRequestWithoutPersisting_WhenCsamMentioned()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/reports",
            new { targetType = "post", targetId = Guid.NewGuid(), reason = "otro", details = "Contenido con abuso infantil / CSAM" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ContentReportRepository.DidNotReceive().TryAddAsync(Arg.Any<ContentReport>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateReport_ReturnsConflict_WhenUserAlreadyReportedTargetWithin24Hours()
    {
        (string token, User user) = CreateRegisteredToken();
        Guid postId = Guid.NewGuid();
        _factory.ReportTargetService.GetTargetInfoAsync(ReportTargetType.Post, postId, Arg.Any<CancellationToken>())
            .Returns(new ReportTargetInfo(true, Guid.NewGuid()));
        _factory.ContentReportRepository.ExistsRecentAsync(user.Id, ReportTargetType.Post, postId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/reports",
            new { targetType = "post", targetId = postId, reason = "spam", details = "Repetido" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
