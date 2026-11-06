using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.ModerateImage;
using Sensus.Application.Media.DTOs;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Endpoints;

public class MediaEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MediaEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Moderate_WithoutAuth_ShouldReturnUnauthorized()
    {
        var command = new ModerateImageCommand(Convert.ToBase64String([1, 2, 3]));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/moderate")
        {
            Content = JsonContent.Create(command)
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Moderate_WithGuestToken_ShouldReturnOk()
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (guestToken, _) = tokenGenerator.GenerateGuestToken();

        var command = new ModerateImageCommand(Convert.ToBase64String([1, 2, 3, 4]));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/moderate")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ModerateImageResponseDto>();
        dto.Should().NotBeNull();
        dto!.Approved.Should().BeTrue();
    }
}
