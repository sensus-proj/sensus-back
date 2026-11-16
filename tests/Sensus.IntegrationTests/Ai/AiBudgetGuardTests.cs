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
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
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
            _cacheService,
            Options.Create(_openAiSettings),
            NullLogger<AiBudgetGuard>.Instance);
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenRedisUnavailable_FailsClosed()
    {
        // Arrange: Redis está caído o deshabilitado
        _cacheService.IsAvailable.Returns(false);
        var sut = CreateSut();

        // Act
        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        // Assert: Comportamiento fail-closed para proteger el presupuesto EX-001
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenRedisUnavailable_ReturnsFalse()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(false);
        var sut = CreateSut();

        // Act
        var available = await sut.IsReasoningBudgetAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenSpendBelowCap_ReturnsTrue()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(true);
        _cacheService.GetAsync<double?>(Arg.Is<string>(k => k.Contains("reasoning")), Arg.Any<CancellationToken>())
            .Returns(2.40); // 2.40 < 3.00 USD
        var sut = CreateSut();

        // Act
        var available = await sut.IsReasoningBudgetAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsReasoningBudgetAvailableAsync_WhenSpendReachesCap_ReturnsFalse()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(true);
        _cacheService.GetAsync<double?>(Arg.Is<string>(k => k.Contains("reasoning")), Arg.Any<CancellationToken>())
            .Returns(3.00); // 3.00 >= 3.00 USD
        var sut = CreateSut();

        // Act
        var available = await sut.IsReasoningBudgetAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task RecordSpendAsync_WhenReasoningTier_IncrementsBothTotalAndReasoningKeys()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(true);
        var sut = CreateSut();
        var userId = Guid.NewGuid();

        // Act
        await sut.RecordSpendAsync(userId, 0.016m, AiRoutingTier.Reasoning);

        // Assert
        await _cacheService.Received(1).IncrementByFloatAsync(
            Arg.Is<string>(k => k.Contains("spend:") && !k.Contains("reasoning")),
            Arg.Is<double>(v => Math.Abs(v - 0.016) < 0.0001),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());

        await _cacheService.Received(1).IncrementByFloatAsync(
            Arg.Is<string>(k => k.Contains("spend:reasoning:")),
            Arg.Is<double>(v => Math.Abs(v - 0.016) < 0.0001),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());

        await _cacheService.Received(1).IncrementAsync(
            Arg.Is<string>(k => k.Contains(userId.ToString())),
            1,
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenTotalBudgetExceeded_ReturnsFalse()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(true);
        _cacheService.GetAsync<long?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((long?)5); // 5 mensajes hoy (< 20)
        _cacheService.GetAsync<double?>(Arg.Is<string>(k => !k.Contains("reasoning")), Arg.Any<CancellationToken>())
            .Returns(4.999); // 4.999 + 0.002 = 5.001 > 5.00 USD
        var sut = CreateSut();

        // Act
        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.002m);

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteTurnAsync_WhenDailyLimitReached_ReturnsFalse()
    {
        // Arrange
        _cacheService.IsAvailable.Returns(true);
        _cacheService.GetAsync<long?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((long?)20); // 20 mensajes hoy
        var sut = CreateSut();

        // Act
        var canExecute = await sut.CanExecuteTurnAsync(Guid.NewGuid(), 0.001m);

        // Assert
        canExecute.Should().BeFalse();
    }
}
