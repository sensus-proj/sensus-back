using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Ai.Budget;
using Sensus.Infrastructure.Services;

namespace Sensus.IntegrationTests.Ai;

public sealed class DynamoDbLocalBudgetCounterStoreTests : IAsyncLifetime, IDisposable
{
    private const int DynamoDbPort = 8000;
    private const string TableName = "sensus-sensi-budget-test";
    private readonly IContainer _container = new ContainerBuilder("amazon/dynamodb-local:2.6.1")
        .WithCommand("-jar", "DynamoDBLocal.jar", "-inMemory", "-sharedDb")
        .WithPortBinding(DynamoDbPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(DynamoDbPort))
        .Build();
    private readonly TestClock _clock = new();
    private AmazonDynamoDBClient? _client;
    private DynamoDbBudgetCounterStore _store = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var endpoint = new UriBuilder("http", _container.Hostname, _container.GetMappedPublicPort(DynamoDbPort));
        _client = new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), new AmazonDynamoDBConfig
        {
            ServiceURL = endpoint.Uri.AbsoluteUri,
            AuthenticationRegion = "us-east-1",
            Timeout = TimeSpan.FromSeconds(5)
        });

        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = TableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)]
        });
        _store = new DynamoDbBudgetCounterStore(_client,
            Options.Create(new AiBudgetStoreSettings { TableName = TableName }),
            NullLogger<DynamoDbBudgetCounterStore>.Instance, _clock);
    }

    [Fact]
    public async Task Counter_PreservesConcurrentIncrementsAndExpiresAtOriginalDeadline()
    {
        const string key = "spend:202611";
        var ttl = TimeSpan.FromDays(2);
        Assert.Equal(0d, await _store.GetAsync(key));

        double?[] totals = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => _store.IncrementAsync(key, 0.25, ttl)));

        Assert.Equal(Enumerable.Range(1, 12).Select(i => (double?)(i * 0.25)), totals.Order());
        Assert.Equal(3d, await _store.GetAsync(key));

        _clock.UtcNow += TimeSpan.FromDays(1);
        Assert.Equal(3.25, await _store.IncrementAsync(key, 0.25, ttl));
        _clock.UtcNow += TimeSpan.FromDays(1);
        Assert.Equal(0d, await _store.GetAsync(key));
    }

    [Fact]
    public async Task Guard_RecordsSpendAndRejectsQuotaBudgetAndInvalidCounters()
    {
        var guard = new AiBudgetGuard(_store, Options.Create(new OpenAiSettings
        {
            Sensi = new SensiSettings
            {
                TotalBudgetCapUsd = 5,
                ReasoningBudgetCapUsd = 3,
                AlertThresholdRatio = 0.8,
                DailyMessageLimitPerUser = 1
            }
        }), NullLogger<AiBudgetGuard>.Instance, _clock);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        Assert.True(await guard.CanExecuteTurnAsync(firstUser, 3m));
        await guard.RecordSpendAsync(firstUser, 3m, AiRoutingTier.Reasoning);

        Assert.Equal(3d, await _store.GetAsync("spend:202611"));
        Assert.Equal(3d, await _store.GetAsync("spend:reasoning:202611"));
        Assert.Equal(1, await guard.GetTodayMessageCountAsync(firstUser));
        Assert.False(await guard.IsReasoningBudgetAvailableAsync());
        Assert.False(await guard.CanExecuteTurnAsync(firstUser, 0.01m));
        Assert.False(await guard.CanExecuteTurnAsync(secondUser, 2.01m));
        Assert.True(await guard.CanExecuteTurnAsync(secondUser, 2m));

        await _client!.PutItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = $"daily:{secondUser}:20261122" }
        });
        Assert.False(await guard.CanExecuteTurnAsync(secondUser, 0.01m));
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public void Dispose() => _client?.Dispose();

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
