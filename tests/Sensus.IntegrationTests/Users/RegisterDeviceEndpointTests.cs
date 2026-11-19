using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RegisterDevice;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Users;

public sealed class RegisterDeviceEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(null, true, PushResult.Accepted, HttpStatusCode.Unauthorized)]
    [InlineData("guest", true, PushResult.Accepted, HttpStatusCode.Forbidden)]
    [InlineData("registered", false, PushResult.Accepted, HttpStatusCode.BadRequest)]
    [InlineData("registered", true, PushResult.InvalidToken, HttpStatusCode.BadRequest)]
    [InlineData("registered", true, PushResult.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData("registered", true, PushResult.Accepted, HttpStatusCode.OK)]
    public async Task PutDeviceToken_EnforcesAuthorizationAndMapsProviderResult(
        string? session, bool validToken, PushResult validation, HttpStatusCode expected)
    {
        var dispatcher = Substitute.For<IPushDispatcher>();
        dispatcher.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(validation);
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushDispatcher>();
                services.AddSingleton(dispatcher);
                services.RemoveAll<IDeviceRepository>();
                services.AddSingleton(Substitute.For<IDeviceRepository>());
            }));
        using HttpClient client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/device-token");
        request.Content = JsonContent.Create(new RegisterDeviceCommand(
            "device-test", "Android", validToken ? "test-device-registration-12345" : "short"));

        if (session is not null)
        {
            string token;
            if (session == "guest")
            {
                using HttpResponseMessage guest = await client.PostAsync("/api/v1/auth/guest", null);
                guest.EnsureSuccessStatusCode();
                var sessionDto = await guest.Content.ReadFromJsonAsync<Sensus.Application.Users.DTOs.GuestSessionResponseDto>();
                token = sessionDto!.Token;
            }
            else
            {
                User user = User.Create("Test User", "device@example.test", "hashed-pw", true, DateTime.UtcNow).Value;
                factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
                (token, _) = application.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.ServiceUnavailable)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
