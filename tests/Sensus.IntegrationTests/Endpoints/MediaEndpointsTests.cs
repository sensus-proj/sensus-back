using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Application.Common.Interfaces;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Endpoints;

public sealed class MediaEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandaloneModerationRoute_IsUnavailable(bool authenticated)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/moderate");
        if (authenticated)
        {
            var tokenGenerator = factory.Services.GetRequiredService<IJwtTokenGenerator>();
            var (token, _) = tokenGenerator.GenerateGuestToken();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
