using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Infrastructure.Ai.Budget;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas contra DynamoDB Local real para <see cref="DynamoDbBudgetCounterStore"/> (Spec 049, T049-09).
/// Se ejecutan cuando <c>DYNAMODB_LOCAL_ENDPOINT</c> está definida (el job <c>integration-tests</c> de CI
/// levanta <c>amazon/dynamodb-local:2.6.1</c> en <c>http://localhost:8000</c>).
/// </summary>
public sealed class DynamoDbBudgetCounterStoreIntegrationTests : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset InitialNow = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly string? _endpoint = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_ENDPOINT");
    private readonly string _tableName = $"sensus_budget_test_{Guid.NewGuid():N}";
    private readonly MutableTimeProvider _time = new(InitialNow);
    private AmazonDynamoDBClient? _client;
    private DynamoDbBudgetCounterStore? _store;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            return;
        }

        _client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("localtestkey", "localtestsecret"),
            new AmazonDynamoDBConfig
            {
                ServiceURL = _endpoint,
                Timeout = TimeSpan.FromSeconds(5),
                MaxErrorRetry = 1
            });

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = _tableName,
                    AttributeDefinitions =
                    [
                        new AttributeDefinition(DynamoDbBudgetCounterStore.KeyAttribute, ScalarAttributeType.S)
                    ],
                    KeySchema =
                    [
                        new KeySchemaElement(DynamoDbBudgetCounterStore.KeyAttribute, KeyType.HASH)
                    ],
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });

                await _client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = _tableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification
                    {
                        AttributeName = DynamoDbBudgetCounterStore.ExpiresAttribute,
                        Enabled = true
                    }
                });

                _store = new DynamoDbBudgetCounterStore(
                    _client,
                    Options.Create(new AiBudgetStoreSettings { TableName = _tableName }),
                    NullLogger<DynamoDbBudgetCounterStore>.Instance,
                    _time);
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.DeleteTableAsync(new DeleteTableRequest { TableName = _tableName });
        }
        catch (ResourceNotFoundException)
        {
            // Tabla ya eliminada o no creada
        }
        finally
        {
            _client.Dispose();
            _client = null;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private bool Enabled => _store is not null;

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsZero()
    {
        if (!Enabled) return;

        double? value = await _store!.GetAsync("missing:key");

        value.Should().Be(0d);
    }

    [Fact]
    public async Task IncrementAsync_AccumulatesAtomicallyUnderConcurrency_AndPreservesInitialExpiration()
    {
        if (!Enabled) return;

        string key = $"spend:concurrent:{Guid.NewGuid():N}";
        Task<double?>[] updates = Enumerable.Range(0, 10)
            .Select(_ => _store!.IncrementAsync(key, 0.1d, TimeSpan.FromHours(1)))
            .ToArray();

        double?[] results = await Task.WhenAll(updates);
        double? total = await _store!.GetAsync(key);

        results.Should().OnlyContain(v => v.HasValue);
        total.Should().BeApproximately(1.0d, 1e-6);

        var rawItem = await _client!.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            ConsistentRead = true,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbBudgetCounterStore.KeyAttribute] = new() { S = key }
            }
        });
        long expectedExp = InitialNow.AddHours(1).ToUnixTimeSeconds();
        rawItem.Item[DynamoDbBudgetCounterStore.ExpiresAttribute].N.Should().Be(
            expectedExp.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GetAsync_WhenCounterExpiredBeforeTtlPurge_ReturnsZero()
    {
        if (!Enabled) return;

        string key = $"daily:expiring:{Guid.NewGuid():N}";
        await _store!.IncrementAsync(key, 3d, TimeSpan.FromMinutes(30));

        (await _store.GetAsync(key)).Should().Be(3d);

        _time.SetUtcNow(InitialNow.AddMinutes(31));

        (await _store.GetAsync(key)).Should().Be(0d);
    }

    [Fact]
    public async Task Table_HasTtlEnabledOnExpiresAtAttribute()
    {
        if (!Enabled) return;

        var ttl = await _client!.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
        {
            TableName = _tableName
        });

        ttl.TimeToLiveDescription.AttributeName.Should().Be(DynamoDbBudgetCounterStore.ExpiresAttribute);
        ttl.TimeToLiveDescription.TimeToLiveStatus.Should().Be(TimeToLiveStatus.ENABLED);
    }

    private sealed class MutableTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;

        public void SetUtcNow(DateTimeOffset value) => _now = value;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
