using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Queries.GetProfile;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Queries;

public sealed class GetProfileQueryHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        _currentUserService.UserId.Returns((string?)null);

        var handler = new GetProfileQueryHandler(_userRepository, _currentUserService, _cacheService);
        var result = await handler.Handle(new GetProfileQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenProfileInCache_ReturnsCachedWithoutHittingDatabase()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var cachedDto = new UserDto(
            userId,
            "Cached User",
            "cached@sensus.com",
            [RoleType.Consumidor],
            true,
            DateTime.UtcNow,
            null,
            "https://cdn.cloudinary.com/pic.jpg");

        _cacheService.GetAsync<UserDto>($"sensus:users:{userId}:profile", Arg.Any<CancellationToken>())
            .Returns(cachedDto);

        var handler = new GetProfileQueryHandler(_userRepository, _currentUserService, _cacheService);
        var result = await handler.Handle(new GetProfileQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NombreCompleto.Should().Be("Cached User");
        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotInCache_FetchesFromDatabaseAndCaches()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _cacheService.GetAsync<UserDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);

        var user = User.Create(
            "DB User",
            "db@sensus.com",
            "hashed_secret",
            aceptoTerminos: true,
            fechaRegistro: DateTime.UtcNow).Value;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetProfileQueryHandler(_userRepository, _currentUserService, _cacheService);
        var result = await handler.Handle(new GetProfileQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NombreCompleto.Should().Be("DB User");

        await _cacheService.Received(1).SetForEntityAsync(
            $"sensus:users:{userId}:profile",
            Arg.Is<UserDto>(d => d.NombreCompleto == "DB User"),
            "UserProfile",
            Arg.Any<CancellationToken>());
    }
}
