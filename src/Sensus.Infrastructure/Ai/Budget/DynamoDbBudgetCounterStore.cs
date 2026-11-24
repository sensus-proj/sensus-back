using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Ai.Budget;

/// <summary>
/// Contadores atómicos de presupuesto y cuota en Amazon DynamoDB (Spec 049, ADR 0013).
/// Tabla con clave <c>pk</c>, atributo numérico <c>amount</c> y TTL nativo en <c>expiresAt</c> (segundos Unix).
/// Cualquier fallo se traduce en <c>null</c> (fail-closed); los logs no incluyen claves ni valores (NFR-049-03).
/// </summary>
public sealed class DynamoDbBudgetCounterStore : IBudgetCounterStore
{
    internal const string KeyAttribute = "pk";
    internal const string AmountAttribute = "amount";
    internal const string ExpiresAttribute = "expiresAt";

    private readonly IAmazonDynamoDB _client;
    private readonly AiBudgetStoreSettings _settings;
    private readonly ILogger<DynamoDbBudgetCounterStore> _logger;
    private readonly TimeProvider _timeProvider;

    public DynamoDbBudgetCounterStore(
        IAmazonDynamoDB client,
        IOptions<AiBudgetStoreSettings> settings,
        ILogger<DynamoDbBudgetCounterStore> logger,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.TableName);

    public async Task<double?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            var response = await _client.GetItemAsync(
                new GetItemRequest
                {
                    TableName = _settings.TableName,
                    ConsistentRead = true,
                    Key = new Dictionary<string, AttributeValue> { [KeyAttribute] = new() { S = key } },
                    ProjectionExpression = "#k, #a, #e",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#k"] = KeyAttribute,
                        ["#a"] = AmountAttribute,
                        ["#e"] = ExpiresAttribute
                    }
                },
                cancellationToken);

            if (!response.IsItemSet || response.Item.Count == 0)
            {
                return 0d;
            }

            if (!response.Item.TryGetValue(ExpiresAttribute, out var expires)
                || !long.TryParse(expires.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAt)
                || expiresAt <= 0)
            {
                return null;
            }

            // DynamoDB puede tardar en purgar: un contador vencido cuenta como 0 (FR-049-07).
            if (expiresAt <= _timeProvider.GetUtcNow().ToUnixTimeSeconds())
            {
                return 0d;
            }

            return response.Item.TryGetValue(AmountAttribute, out var amount) ? ParseAmount(amount.N) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fallo al leer un contador de presupuesto de Sensi IA en DynamoDB ({ErrorType}).", ex.GetType().Name);
            return null;
        }
    }

    public async Task<double?> IncrementAsync(
        string key,
        double amount,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            var expiresAt = _timeProvider.GetUtcNow().Add(expiration).ToUnixTimeSeconds();
            var response = await _client.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _settings.TableName,
                    Key = new Dictionary<string, AttributeValue> { [KeyAttribute] = new() { S = key } },
                    UpdateExpression = "SET #e = if_not_exists(#e, :exp) ADD #a :inc",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#a"] = AmountAttribute,
                        ["#e"] = ExpiresAttribute
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":inc"] = new() { N = ((decimal)amount).ToString(CultureInfo.InvariantCulture) },
                        [":exp"] = new() { N = expiresAt.ToString(CultureInfo.InvariantCulture) }
                    },
                    ReturnValues = ReturnValue.UPDATED_NEW
                },
                cancellationToken);

            return response.Attributes.TryGetValue(AmountAttribute, out var total) ? ParseAmount(total.N) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fallo al incrementar un contador de presupuesto de Sensi IA en DynamoDB ({ErrorType}).", ex.GetType().Name);
            return null;
        }
    }

    private static double? ParseAmount(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? (double)value
            : null;
}
