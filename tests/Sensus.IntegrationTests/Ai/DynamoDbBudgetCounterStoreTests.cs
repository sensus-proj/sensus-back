using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Infrastructure.Ai.Budget;
using Xunit;

namespace Sensus.IntegrationTests.Ai;

/// <summary>
/// Pruebas de <see cref="DynamoDbBudgetCounterStore"/> con doble de <see cref="IAmazonDynamoDB"/> (Spec 049).
/// Las pruebas contra el motor real corresponden a <see cref="DynamoDbLocalBudgetCounterStoreTests"/> (T049-09).
/// </summary>
public sealed class DynamoDbBudgetCounterStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _client = Substitute.For<IAmazonDynamoDB>();
    private readonly FakeTimeProvider _time = new(Now);

    private DynamoDbBudgetCounterStore CreateSut(string tableName = "sensus-sensi-budget-test") =>
        new(
            _client,
            Options.Create(new AiBudgetStoreSettings { TableName = tableName }),
            NullLogger<DynamoDbBudgetCounterStore>.Instance,
            _time);

    [Fact]
    public void IsAvailable_IsFalseWithoutTableName_AndTrueWithIt()
    {
        CreateSut(tableName: "").IsAvailable.Should().BeFalse();
        CreateSut().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WithoutTableName_ReturnsNullAndNeverCallsDynamoDb()
    {
        var result = await CreateSut(tableName: " ").GetAsync("spend:202611");

        result.Should().BeNull();
        await _client.DidNotReceiveWithAnyArgs().GetItemAsync(default(GetItemRequest)!, default);
    }

    [Fact]
    public async Task GetAsync_WhenItemMissing_ReturnsZero()
    {
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        (await CreateSut().GetAsync("spend:202611")).Should().Be(0d);
    }

    [Fact]
    public async Task GetAsync_ReturnsAmount_UsingConsistentReadOnPrimaryKey()
    {
        GetItemRequest? captured = null;
        _client.GetItemAsync(Arg.Do<GetItemRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["amount"] = new() { N = "1.2345" },
                    ["expiresAt"] = new() { N = Epoch(3600) }
                }
            });

        var result = await CreateSut().GetAsync("spend:202611");

        result.Should().BeApproximately(1.2345, 1e-9);
        captured.Should().NotBeNull();
        captured!.TableName.Should().Be("sensus-sensi-budget-test");
        captured.ConsistentRead.Should().BeTrue();
        captured.Key["pk"].S.Should().Be("spend:202611");
    }

    [Fact]
    public async Task GetAsync_WhenCounterExpiredButNotPurged_ReturnsZero()
    {
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["amount"] = new() { N = "4.9" },
                    ["expiresAt"] = new() { N = Epoch(-1) }
                }
            });

        (await CreateSut().GetAsync("spend:202610")).Should().Be(0d);
    }

    [Fact]
    public async Task GetAsync_WhenAmountMalformed_ReturnsNull()
    {
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue> { ["amount"] = new() { N = "not-a-number" } }
            });

        (await CreateSut().GetAsync("spend:202611")).Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(AmazonDynamoDBException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(HttpRequestException))]
    public async Task GetAsync_WhenSdkFails_ReturnsNull(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(exception);

        (await CreateSut().GetAsync("spend:202611")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => CreateSut().GetAsync("spend:202611", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task IncrementAsync_SendsAtomicAddWithTtlAndReturnsNewTotal()
    {
        UpdateItemRequest? captured = null;
        _client.UpdateItemAsync(Arg.Do<UpdateItemRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse
            {
                Attributes = new Dictionary<string, AttributeValue> { ["amount"] = new() { N = "0.017" } }
            });

        var result = await CreateSut().IncrementAsync("spend:202611", 0.016, TimeSpan.FromDays(45));

        result.Should().BeApproximately(0.017, 1e-9);
        captured.Should().NotBeNull();
        captured!.TableName.Should().Be("sensus-sensi-budget-test");
        captured.Key["pk"].S.Should().Be("spend:202611");
        captured.UpdateExpression.Should().Contain("ADD").And.Contain("if_not_exists");
        captured.ExpressionAttributeValues[":inc"].N.Should().Be("0.016");
        captured.ExpressionAttributeValues[":exp"].N.Should().Be(Epoch(45 * 86400));
        captured.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }

    [Fact]
    public async Task IncrementAsync_WithoutTableName_ReturnsNullAndNeverCallsDynamoDb()
    {
        var result = await CreateSut(tableName: "").IncrementAsync("daily:x", 1, TimeSpan.FromDays(2));

        result.Should().BeNull();
        await _client.DidNotReceiveWithAnyArgs().UpdateItemAsync(default(UpdateItemRequest)!, default);
    }

    [Fact]
    public async Task IncrementAsync_WhenSdkFails_ReturnsNull()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonDynamoDBException("throttled"));

        (await CreateSut().IncrementAsync("daily:x", 1, TimeSpan.FromDays(2))).Should().BeNull();
    }

    [Fact]
    public async Task IncrementAsync_WhenResponseHasNoAmount_ReturnsNull()
    {
        _client.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse { Attributes = new Dictionary<string, AttributeValue>() });

        (await CreateSut().IncrementAsync("daily:x", 1, TimeSpan.FromDays(2))).Should().BeNull();
    }

    private static string Epoch(long offsetSeconds) =>
        (Now.ToUnixTimeSeconds() + offsetSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
