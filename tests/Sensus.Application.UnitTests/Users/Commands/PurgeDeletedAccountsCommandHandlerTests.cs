using FluentAssertions;
using NSubstitute;
using Sensus.Application.Users.Commands.PurgeDeletedAccounts;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

/// <summary>Purga diferida y segura de cuentas dadas de baja (Spec 060, FR-060-04 y FR-060-06).</summary>
public class PurgeDeletedAccountsCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly PurgeDeletedAccountsCommandHandler _handler;
    private readonly DateTime _cutoff = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    public PurgeDeletedAccountsCommandHandlerTests()
    {
        _handler = new PurgeDeletedAccountsCommandHandler(_userRepository);
    }

    [Fact]
    public async Task Handle_ShouldPurgeOnlyExpiredDeletedAccounts_WithInjectedCutoff()
    {
        Guid expired = Guid.NewGuid();
        _userRepository.GetIdsDeletedBeforeAsync(_cutoff, PurgeDeletedAccountsCommandHandler.BatchSize, Arg.Any<CancellationToken>())
            .Returns(new[] { expired });
        _userRepository.PurgeDeletedAsync(expired, _cutoff, Arg.Any<CancellationToken>()).Returns(true);

        Result<PurgeDeletedAccountsResult> result =
            await _handler.Handle(new PurgeDeletedAccountsCommand(_cutoff), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(1);
        await _userRepository.Received(1).GetIdsDeletedBeforeAsync(
            _cutoff, PurgeDeletedAccountsCommandHandler.BatchSize, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).PurgeDeletedAsync(expired, _cutoff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotPurgeAnything_WhenThereAreNoDeletedAccounts()
    {
        _userRepository.GetIdsDeletedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        Result<PurgeDeletedAccountsResult> result =
            await _handler.Handle(new PurgeDeletedAccountsCommand(_cutoff), CancellationToken.None);

        result.Value.PurgedCount.Should().Be(0);
        await _userRepository.DidNotReceiveWithAnyArgs().PurgeDeletedAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAlreadyPurgedAccounts_Idempotence()
    {
        Guid alreadyPurged = Guid.NewGuid();
        _userRepository.GetIdsDeletedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { alreadyPurged });
        _userRepository.PurgeDeletedAsync(alreadyPurged, _cutoff, Arg.Any<CancellationToken>()).Returns(false);

        Result<PurgeDeletedAccountsResult> result =
            await _handler.Handle(new PurgeDeletedAccountsCommand(_cutoff), CancellationToken.None);

        result.Value.PurgedCount.Should().Be(0);
    }
}
