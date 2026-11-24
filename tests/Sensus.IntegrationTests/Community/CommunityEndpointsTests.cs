using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendsActivity;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Community;

public sealed class CommunityEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Path = "/api/v1/community/friends-activity";

    [Fact]
    public async Task FriendsActivity_RequiresRegisteredUser()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var (token, _) = factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var guest = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, guest.StatusCode);
    }

    [Fact]
    public async Task FriendsActivity_ValidatesPageAndIncludesOnlyAcceptedFriendIds()
    {
        var now = DateTime.UtcNow;
        var user = User.Create("Persona Sensus", $"social-{Guid.NewGuid():N}@example.test", "hash", true, now).Value;
        factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var friendId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var friends = Substitute.For<IFriendRequestRepository>();
        var accepted = FriendRequest.Rehydrate(Guid.NewGuid(), friendId, "Amiga", "FRIEND", user.Id,
            "Persona", "VIEWER", "QR", FriendRequestStatus.Accepted, now, now);
        var pending = FriendRequest.Create(user.Id, "Persona", "VIEWER", pendingId, "Pendiente", "PEND", "QR", now).Value;
        friends.GetForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(new[] { accepted, pending });

        var reader = Substitute.For<ICommunityActivityReader>();
        reader.ReadAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids.Count == 2 && ids.Contains(user.Id) && ids.Contains(friendId)),
            2, 1, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CommunityActivityItemDto>([], 1, 2, 1));
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices((_, services) =>
        {
            services.Replace(ServiceDescriptor.Singleton(friends));
            services.Replace(ServiceDescriptor.Singleton(reader));
        }));
        using var client = app.CreateClient();
        var (token, _) = factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var badResponse = await client.GetAsync($"{Path}?pageSize=101");
        using var response = await client.GetAsync($"{Path}?page=2&limit=1");

        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<CommunityActivityItemDto>>();
        Assert.NotNull(page);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(1, page.TotalCount);
    }
}
