using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Ai.Budget;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Ai;

public sealed class AiBudgetGuardTests
{
    private readonly IBudgetCounterStore _store = Substitute.For<IBudgetCounterStore>();
    private readonly OpenAiSettings _openAiSettings;

    public AiBudgetGuardTests()
    {
        _openAiSettings = new OpenAiSettings
        {
            Sensi = new SensiSettings
            {
                TotalBudgetCapUsd = 5.00,
                ReasoningBudgetCapUsd = 3.00,
                AlertThresholdRatio = 0.80,
                DailyMessageLimitPerUser = 20
            }
        };
    }

    private AiBudgetGuard CreateSut()
    {
        return new AiBudgetGuard(
            _store,
            Options.Create(_openAiSettings),
            NullLogger<AiBudgetGuard>.Instance);
    }

    private void SetupCounters(double? daily, double? monthly)
    {
        _store.IsAvailable.Returns(true);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("daily:")), Arg.Any<CancellationToken>()).Returns(daily);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("spend:") && !k.StartsWith("spend:reasoning:")), Arg.Any<CancellationToken>())
            .Returns(monthly);
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenStoreUnavailable_FailsClosed()
    {
        _store.IsAvailable.Returns(false);
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenDailyReadFails_FailsClosed()
    {
        SetupCounters(daily: null, monthly: 0d);
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenMonthlyReadFails_FailsClosed()
    {
        SetupCounters(daily: 1d, monthly: null);
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenUnderBudgetAndQuota_ReturnsTrue()
    {
        SetupCounters(daily: 3d, monthly: 0.25);
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.001m);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenTotalBudgetExceeded_ReturnsFalse()
    {
        SetupCounters(daily: 5d, monthly: 4.999); // 4.999 + 0.002 = 5.001 > 5.00 USD
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenDailyLimitReached_ReturnsFalse()
    {
        SetupCounters(daily: 20d, monthly: 0d);
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.001m);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenAlertThresholdReached_StillAllowsTurn()
    {
        SetupCounters(daily: 1d, monthly: 4.10); // >= 80 % de 5.00 y por debajo del techo
        var sut = CreateSut();

        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.001m);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenStoreUnavailable_ReturnsFalse()
    {
        _store.IsAvailable.Returns(false);
        var sut = CreateSut();

        (await sut.IsReasoningBudgetAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenReadFails_ReturnsFalse()
    {
        _store.IsAvailable.Returns(true);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("spend:reasoning:")), Arg.Any<CancellationToken>()).Returns((double?)null);
        var sut = CreateSut();

        (await sut.IsReasoningBudgetAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenSpendBelowCap_ReturnsTrue()
    {
        _store.IsAvailable.Returns(true);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("spend:reasoning:")), Arg.Any<CancellationToken>()).Returns(2.40);
        var sut = CreateSut();

        (await sut.IsReasoningBudgetAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenSpendReachesCap_ReturnsFalse()
    {
        _store.IsAvailable.Returns(true);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("spend:reasoning:")), Arg.Any<CancellationToken>()).Returns(3.00);
        var sut = CreateSut();

        (await sut.IsReasoningBudgetAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task RecordSpendAsync_WhenReasoningTier_IncrementsTotalReasoningAndDailyKeys()
    {
        _store.IsAvailable.Returns(true);
        _store.IncrementAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(1d);
        var sut = CreateSut();
        var userId = Guid.NewGuid();

        await sut.RecordSpendAsync(userId, 0.016m, AiRoutingTier.Reasoning);

        await _store.Received(1).IncrementAsync(
            Arg.Is<string>(k => k.StartsWith("spend:") && !k.StartsWith("spend:reasoning:")),
            Arg.Is<double>(v => Math.Abs(v - 0.016) < 0.0001),
            TimeSpan.FromDays(45),
            Arg.Any<CancellationToken>());
        await _store.Received(1).IncrementAsync(
            Arg.Is<string>(k => k.StartsWith("spend:reasoning:")),
            Arg.Is<double>(v => Math.Abs(v - 0.016) < 0.0001),
            TimeSpan.FromDays(45),
            Arg.Any<CancellationToken>());
        await _store.Received(1).IncrementAsync(
            Arg.Is<string>(k => k.StartsWith("daily:" + userId)),
            1d,
            TimeSpan.FromDays(2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordSpendAsync_WhenFastTier_DoesNotTouchReasoningKey()
    {
        _store.IsAvailable.Returns(true);
        _store.IncrementAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(1d);
        var sut = CreateSut();

        await sut.RecordSpendAsync(Guid.NewGuid(), 0.001m, AiRoutingTier.Fast);

        await _store.DidNotReceive().IncrementAsync(
            Arg.Is<string>(k => k.StartsWith("spend:reasoning:")),
            Arg.Any<double>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordSpendAsync_WhenStoreUnavailable_DoesNotThrowNorIncrement()
    {
        _store.IsAvailable.Returns(false);
        var sut = CreateSut();

        var act = () => sut.RecordSpendAsync(Guid.NewGuid(), 0.001m);

        await act.Should().NotThrowAsync();
        await _store.DidNotReceiveWithAnyArgs().IncrementAsync(default!, default, default, default);
    }

    [Fact]
    public async Task RecordSpendAsync_WhenIncrementFails_DoesNotThrow()
    {
        _store.IsAvailable.Returns(true);
        _store.IncrementAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns((double?)null);
        var sut = CreateSut();

        var act = () => sut.RecordSpendAsync(Guid.NewGuid(), 0.001m);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetTodayMessageCountAsync_ReturnsStoredCount_AndZeroWhenUnavailable()
    {
        _store.IsAvailable.Returns(true);
        _store.GetAsync(Arg.Is<string>(k => k.StartsWith("daily:")), Arg.Any<CancellationToken>()).Returns(7d);
        var sut = CreateSut();
        (await sut.GetTodayMessageCountAsync(Guid.NewGuid())).Should().Be(7);

        _store.IsAvailable.Returns(false);
        (await sut.GetTodayMessageCountAsync(Guid.NewGuid())).Should().Be(0);
    }
}
