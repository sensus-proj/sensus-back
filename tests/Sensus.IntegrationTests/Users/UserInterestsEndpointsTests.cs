using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Users;

/// <summary>
/// Pruebas de integración HTTP para Spec 054 (GET/PUT /api/v1/users/me/interests).
/// </summary>
public sealed class UserInterestsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly string[] BakeryCategory = ["Bakery"];
    private static readonly string[] PetsCategory = ["Pets"];
    private static readonly string[] BakeryAndDairyCategories = ["Bakery", "Dairy"];

    private readonly CustomWebApplicationFactory _factory;

    public UserInterestsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.UserInterestsRepository.ClearReceivedCalls();
        _factory.CacheService.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Consumidor", $"user.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateToken(user);
        return (token, user);
    }

    private string CreateGuestToken()
    {
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateGuestToken();
        return token;
    }

    [Fact]
    public async Task InterestsEndpoints_WithoutToken_ShouldReturn401()
    {
        using HttpClient client = _factory.CreateClient();

        (await client.GetAsync("/api/v1/users/me/interests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/v1/users/me/interests", new { categories = BakeryCategory }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InterestsEndpoints_AsGuest_ShouldReturn403()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());

        (await client.GetAsync("/api/v1/users/me/interests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync("/api/v1/users/me/interests", new { categories = BakeryCategory }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyInterests_WhenNoneSaved_ShouldReturnEmptyList()
    {
        (string token, User user) = CreateRegisteredToken();
        _factory.UserInterestsRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((UserInterests?)null);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me/interests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserInterestsDto? dto = await response.Content.ReadFromJsonAsync<UserInterestsDto>();
        dto.Should().NotBeNull();
        dto!.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task PutAndGetMyInterests_ShouldPersistIdempotentlyAndInvalidateCache()
    {
        (string token, User user) = CreateRegisteredToken();
        UserInterests? stored = null;

        _factory.UserInterestsRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        _factory.UserInterestsRepository.When(r => r.UpsertAsync(Arg.Any<UserInterests>(), Arg.Any<CancellationToken>()))
            .Do(call => stored = call.Arg<UserInterests>());

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage put1 = await client.PutAsJsonAsync(
            "/api/v1/users/me/interests",
            new { categories = BakeryAndDairyCategories });
        put1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await put1.Content.ReadFromJsonAsync<UserInterestsDto>())!.Categories.Should().Equal("Bakery", "Dairy");

        // Idempotencia: repetir PUT reemplaza con los mismos datos sin error
        HttpResponseMessage put2 = await client.PutAsJsonAsync(
            "/api/v1/users/me/interests",
            new { categories = BakeryAndDairyCategories });
        put2.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage get = await client.GetAsync("/api/v1/users/me/interests");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadFromJsonAsync<UserInterestsDto>())!.Categories.Should().Equal("Bakery", "Dairy");

        await _factory.CacheService.Received(2).RemoveAsync(FeedCacheKeys.Affinity(user.Id), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("""{"categories":["Moda"]}""")]
    [InlineData("""{"categories":["Bakery","Bakery"]}""")]
    [InlineData("""{"categories":["Grocery","CleaningHome","Perishables","Dairy","Food","Bakery"]}""")]
    public async Task PutMyInterests_WithInvalidPayload_ShouldReturn400WithoutSaving(string json)
    {
        (string token, _) = CreateRegisteredToken();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PutAsync("/api/v1/users/me/interests", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.UserInterestsRepository.DidNotReceiveWithAnyArgs()
            .UpsertAsync(Arg.Any<UserInterests>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutMyInterests_WhenPayloadExceeds1Kb_ShouldReturn400()
    {
        (string token, _) = CreateRegisteredToken();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string oversized = $$"""{"categories":["{{new string('A', 1200)}}"]}""";
        using var content = new StringContent(oversized, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PutAsync("/api/v1/users/me/interests", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutMyInterests_WhenRateLimitExceeded_ShouldReturn429()
    {
        // SensitiveWrite en appsettings.json permite 30 solicitudes/minuto (FR-054-07).
        (string token, _) = CreateRegisteredToken();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.77");

        for (int i = 0; i < 30; i++)
        {
            HttpResponseMessage ok = await client.PutAsJsonAsync(
                "/api/v1/users/me/interests",
                new { categories = BakeryCategory });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        HttpResponseMessage limited = await client.PutAsJsonAsync(
            "/api/v1/users/me/interests",
            new { categories = PetsCategory });
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
