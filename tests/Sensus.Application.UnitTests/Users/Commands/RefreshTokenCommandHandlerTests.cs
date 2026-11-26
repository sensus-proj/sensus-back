using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RefreshToken;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.Application.UnitTests.Users.Commands;

public sealed class RefreshTokenCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ShouldRotateRefreshTokenAndIssueNewJwt_WhenTokenIsActive()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var refreshRepo = new LocalRefreshTokenRepository();
        var jwt = Substitute.For<IJwtTokenGenerator>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var user = User.Create("Ana López", "ana@sensus.app", "hash", true, Now).Value;
        userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        jwt.GenerateToken(user).Returns(("new.access.jwt", Now.AddMinutes(15)));

        const string rawToken = "valid-raw-refresh-token-value-123456";
        string hash = RefreshTokenCommandHandler.HashToken(rawToken);
        var familyId = Guid.NewGuid();
        var initial = RefreshTokenEntity.Create(user.Id, hash, Now.AddMinutes(-5), Now.AddDays(30), familyId).Value;
        await refreshRepo.AddAsync(initial);

        var handler = new RefreshTokenCommandHandler(
            userRepo,
            refreshRepo,
            jwt,
            clock,
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("new.access.jwt");
        result.Value.ExpiresAtUtc.Should().Be(Now.AddMinutes(15));
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBe(rawToken);

        RefreshTokenEntity? oldStored = await refreshRepo.GetByTokenHashAsync(hash);
        oldStored!.UsedAtUtc.Should().Be(Now);
        oldStored.ReplacedByTokenId.Should().NotBeNull();

        RefreshTokenEntity? rotatedStored = await refreshRepo.GetByTokenHashAsync(
            RefreshTokenCommandHandler.HashToken(result.Value.RefreshToken!));
        rotatedStored.Should().NotBeNull();
        rotatedStored!.FamilyId.Should().Be(familyId);
        rotatedStored.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenReusedTokenPresented_ShouldRevokeFamilyAndRotateSecurityStamp()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var refreshRepo = new LocalRefreshTokenRepository();
        var jwt = Substitute.For<IJwtTokenGenerator>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var user = User.Create("Ana López", "ana@sensus.app", "hash", true, Now).Value;
        string originalStamp = user.SecurityStamp;
        userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Guid familyId = Guid.NewGuid();
        const string oldRaw = "reused-old-token";
        const string currentRaw = "legit-current-token";
        var currentToken = RefreshTokenEntity.Create(
            user.Id, RefreshTokenCommandHandler.HashToken(currentRaw), Now.AddMinutes(-1), Now.AddDays(30), familyId).Value;
        var usedToken = RefreshTokenEntity.Create(
            user.Id, RefreshTokenCommandHandler.HashToken(oldRaw), Now.AddMinutes(-10), Now.AddDays(30), familyId).Value;
        usedToken.TryMarkUsed(currentToken.Id, Now.AddMinutes(-1));

        await refreshRepo.AddAsync(usedToken);
        await refreshRepo.AddAsync(currentToken);

        var handler = new RefreshTokenCommandHandler(
            userRepo,
            refreshRepo,
            jwt,
            clock,
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var replayResult = await handler.Handle(new RefreshTokenCommand(oldRaw), CancellationToken.None);

        replayResult.IsFailure.Should().BeTrue();
        replayResult.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        currentToken.RevokedAtUtc.Should().NotBeNull();
        user.SecurityStamp.Should().NotBe(originalStamp);
        await userRepo.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    private sealed class LocalRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshTokenEntity> _items = [];

        public Task AddAsync(RefreshTokenEntity token, CancellationToken cancellationToken = default)
        {
            _items.Add(token);
            return Task.CompletedTask;
        }

        public Task<RefreshTokenEntity?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(t => string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal)));

        public Task<bool> TryMarkAsUsedAsync(Guid tokenId, Guid replacedByTokenId, DateTime usedAtUtc, CancellationToken cancellationToken = default)
        {
            RefreshTokenEntity? match = _items.FirstOrDefault(t => t.Id == tokenId);
            return Task.FromResult(match is not null && match.TryMarkUsed(replacedByTokenId, usedAtUtc));
        }

        public Task<int> RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (RefreshTokenEntity token in _items.Where(t => t.FamilyId == familyId && t.RevokedAtUtc is null))
            {
                token.Revoke(revokedAtUtc, reason);
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> RevokeAllByUserAsync(Guid userId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (RefreshTokenEntity token in _items.Where(t => t.UserId == userId && t.RevokedAtUtc is null))
            {
                token.Revoke(revokedAtUtc, reason);
                count++;
            }

            return Task.FromResult(count);
        }
    }
}
