using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo;

/// <summary>
/// Contexto de conexión hacia MongoDB Atlas.
/// </summary>
public sealed class MongoDbContext
{
    private const string ClaimPushReceiptsCollection = "claim_push_receipts";
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoDbContext> _logger;
    private readonly MongoDbSettings _settings;

    public MongoDbContext(
        IOptions<MongoDbSettings> settings,
        ILogger<MongoDbContext> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var resolvedConnectionString = ResolveConnectionString(_settings.ConnectionString);
        if (string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            _logger.LogWarning("MongoDB ConnectionString no está configurada.");
            _database = null;
        }
        else
        {
            _settings.ConnectionString = resolvedConnectionString;
            var client = new MongoClient(resolvedConnectionString);
            _database = client.GetDatabase(_settings.DatabaseName);
        }

        ConfigureIndexes();
    }

    private static string? ResolveConnectionString(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && !configured.StartsWith("${", StringComparison.Ordinal))
        {
            return configured;
        }

        return Environment.GetEnvironmentVariable("MongoDbSettings__ConnectionString")
               ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
               ?? Environment.GetEnvironmentVariable("MONGO_URI");
    }

    public IMongoCollection<UserDocument> Users =>
        _database?.GetCollection<UserDocument>(_settings.UsersCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<ComercioDocument> Comercios =>
        _database?.GetCollection<ComercioDocument>(_settings.ComerciosCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<PasswordResetTokenDocument> PasswordResetTokens =>
        _database?.GetCollection<PasswordResetTokenDocument>(_settings.PasswordResetTokensCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferDocument> Offers =>
        _database?.GetCollection<OfferDocument>(_settings.OffersCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferClaimDocument> OfferClaims =>
        _database?.GetCollection<OfferClaimDocument>(_settings.OfferClaimsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<ClaimPushReceiptDocument> ClaimPushReceipts =>
        _database?.GetCollection<ClaimPushReceiptDocument>(ClaimPushReceiptsCollection)
        ?? throw new InvalidOperationException("MongoDB ConnectionString is not configured.");

    public IMongoCollection<NotificationLocationDocument> NotificationLocations =>
        _database?.GetCollection<NotificationLocationDocument>("notification_locations")
        ?? throw new InvalidOperationException("MongoDB ConnectionString is not configured.");

    public IMongoCollection<OfferViewDocument> OfferViews =>
        _database?.GetCollection<OfferViewDocument>(_settings.OfferViewsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<MediaAssetDocument> MediaAssets =>
        _database?.GetCollection<MediaAssetDocument>(_settings.MediaAssetsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<DeviceDocument> Devices =>
        _database?.GetCollection<DeviceDocument>(_settings.DevicesCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<NotificationSettingsDocument> NotificationSettings =>
        _database?.GetCollection<NotificationSettingsDocument>(_settings.NotificationSettingsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<AiConversationDocument> AiConversations =>
        _database?.GetCollection<AiConversationDocument>(_settings.AiConversationsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<AiMessageDocument> AiMessages =>
        _database?.GetCollection<AiMessageDocument>(_settings.AiMessagesCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<AiTraceLogDocument> AiTraceLogs =>
        _database?.GetCollection<AiTraceLogDocument>(_settings.AiTraceLogsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<FriendRequestDocument> FriendRequests =>
        _database?.GetCollection<FriendRequestDocument>(_settings.FriendRequestsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<SharedOfferDocument> SharedOffers =>
        _database?.GetCollection<SharedOfferDocument>(_settings.SharedOffersCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<SanctionDocument> Sanctions =>
        _database?.GetCollection<SanctionDocument>(_settings.SanctionsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferLikeDocument> OfferLikes =>
        _database?.GetCollection<OfferLikeDocument>("offer_likes")
        ?? throw new InvalidOperationException("MongoDB ConnectionString is not configured.");

    public IMongoCollection<StoreReviewDocument> StoreReviews =>
        _database?.GetCollection<StoreReviewDocument>("store_reviews")
        ?? throw new InvalidOperationException("MongoDB ConnectionString is not configured.");

    public IMongoCollection<BsonDocument> ModerationDecisions =>
        _database?.GetCollection<BsonDocument>("moderation_decisions")
        ?? throw new InvalidOperationException("MongoDB ConnectionString is not configured.");

    /// <summary>
    /// Verifica la disponibilidad de MongoDB para el endpoint de readiness (FR-016-02).
    /// Devuelve false si no hay conexión configurada o si el servidor no responde; nunca expone detalles de conexión.
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return false;
        }

        try
        {
            await _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "El ping a MongoDB falló durante la verificación de readiness.");
            return false;
        }
    }

    private void ConfigureIndexes()
    {
        if (_database is null) return;

        try
        {
            // Spec 020 (NFR-020-04): índices de las consultas de sanciones, declarados y justificados.
            IMongoCollection<SanctionDocument> sanctions = _database.GetCollection<SanctionDocument>(_settings.SanctionsCollectionName);

            // Reincidencia y listado por usuario, del más reciente al más antiguo.
            sanctions.Indexes.CreateOne(new CreateIndexModel<SanctionDocument>(
                Builders<SanctionDocument>.IndexKeys.Ascending(s => s.UserId).Descending(s => s.StartsAtUtc)));

            // Listado general ordenado por fecha.
            sanctions.Indexes.CreateOne(new CreateIndexModel<SanctionDocument>(
                Builders<SanctionDocument>.IndexKeys.Descending(s => s.StartsAtUtc)));

            // Idempotencia: una sola sanción automática por activo (parcial: solo documentos con mediaId).
            sanctions.Indexes.CreateOne(new CreateIndexModel<SanctionDocument>(
                Builders<SanctionDocument>.IndexKeys.Ascending("evidence.mediaId"),
                new CreateIndexOptions<SanctionDocument>
                {
                    Unique = true,
                    PartialFilterExpression = Builders<SanctionDocument>.Filter.Type("evidence.mediaId", MongoDB.Bson.BsonType.String)
                }));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de sanciones ya existen o el clúster aún no está disponible.");
        }


        try
        {
            var indexKeysDefinition = Builders<UserDocument>.IndexKeys.Ascending(u => u.Correo);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<UserDocument>(indexKeysDefinition, indexOptions);

            Users.Indexes.CreateOne(indexModel);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: El índice único de correos ya existe o el clúster aún no está disponible.");
        }

        try
        {
            var comerciosCollection = _database.GetCollection<ComercioDocument>(_settings.ComerciosCollectionName);

            // Índice único para UsuarioId (un usuario tiene un solo comercio activo)
            var userIndexKeys = Builders<ComercioDocument>.IndexKeys.Ascending(c => c.UsuarioId);
            var userIndexOptions = new CreateIndexOptions { Unique = true };
            comerciosCollection.Indexes.CreateOne(new CreateIndexModel<ComercioDocument>(userIndexKeys, userIndexOptions));

            // Índice geoespacial 2dsphere para consultas geográficas
            var geoIndexKeys = Builders<ComercioDocument>.IndexKeys.Geo2DSphere(c => c.Ubicacion);
            comerciosCollection.Indexes.CreateOne(new CreateIndexModel<ComercioDocument>(geoIndexKeys));

            // Índice por categoría para el filtro de ofertas cercanas (FR-007-03)
            var categoryIndexKeys = Builders<ComercioDocument>.IndexKeys.Ascending(c => c.Categoria);
            comerciosCollection.Indexes.CreateOne(new CreateIndexModel<ComercioDocument>(categoryIndexKeys));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de comercios ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var tokensCollection = _database.GetCollection<PasswordResetTokenDocument>(_settings.PasswordResetTokensCollectionName);

            // Índice para TokenHash
            var hashIndexKeys = Builders<PasswordResetTokenDocument>.IndexKeys.Ascending(t => t.TokenHash);
            tokensCollection.Indexes.CreateOne(new CreateIndexModel<PasswordResetTokenDocument>(hashIndexKeys));

            // Índice TTL en ExpiresAtUtc
            var ttlIndexKeys = Builders<PasswordResetTokenDocument>.IndexKeys.Ascending(t => t.ExpiresAtUtc);
            var ttlOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero };
            tokensCollection.Indexes.CreateOne(new CreateIndexModel<PasswordResetTokenDocument>(ttlIndexKeys, ttlOptions));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de tokens ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var offersCollection = _database.GetCollection<OfferDocument>(_settings.OffersCollectionName);

            // Índice compuesto (MerchantId, Status) para inventario y republicación (SE-026, FR-037-01)
            var merchantStatusKeys = Builders<OfferDocument>.IndexKeys
                .Ascending(o => o.MerchantId)
                .Ascending(o => o.Status);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(merchantStatusKeys));

            // Índice compuesto (Status, EndsAt) para control de vigencia y expiración eficiente (SE-022 a SE-025, NFR-007-02)
            var statusEndsAtKeys = Builders<OfferDocument>.IndexKeys
                .Ascending(o => o.Status)
                .Ascending(o => o.EndsAt);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(statusEndsAtKeys));

            // Índice geoespacial 2dsphere para búsqueda de ofertas cercanas
            var offerGeoKeys = Builders<OfferDocument>.IndexKeys.Geo2DSphere(o => o.Ubicacion);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(offerGeoKeys));

            // Índice de texto en (Title, Description) para búsqueda inteligente (spec 041, FR-046-06)
            var textIndexKeys = Builders<OfferDocument>.IndexKeys
                .Text(o => o.Title)
                .Text(o => o.Description);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(textIndexKeys));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de ofertas ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var claimsCollection = _database.GetCollection<OfferClaimDocument>(_settings.OfferClaimsCollectionName);

            // Índice compuesto (UserId, CreatedAtUtc) para historial de apartados por usuario (spec 033, FR-046-07)
            var userClaimsKeys = Builders<OfferClaimDocument>.IndexKeys
                .Ascending(c => c.UserId)
                .Descending(c => c.CreatedAtUtc);
            claimsCollection.Indexes.CreateOne(new CreateIndexModel<OfferClaimDocument>(userClaimsKeys));

            // Un solo apartado activo por usuario y oferta (FR-033-03).
            var activeClaimKeys = Builders<OfferClaimDocument>.IndexKeys
                .Ascending(c => c.UserId)
                .Ascending(c => c.OfferId);
            var activeClaimOptions = new CreateIndexOptions<OfferClaimDocument>
            {
                Unique = true,
                PartialFilterExpression = Builders<OfferClaimDocument>.Filter.Eq(
                    c => c.Status,
                    Sensus.Domain.Enums.OfferClaimStatus.Active.ToString())
            };
            claimsCollection.Indexes.CreateOne(
                new CreateIndexModel<OfferClaimDocument>(activeClaimKeys, activeClaimOptions));

            // El hash del código es único (FR-033-04).
            var codeHashKeys = Builders<OfferClaimDocument>.IndexKeys.Ascending(c => c.CodeHash);
            var codeHashOptions = new CreateIndexOptions { Unique = true };
            claimsCollection.Indexes.CreateOne(
                new CreateIndexModel<OfferClaimDocument>(codeHashKeys, codeHashOptions));

            // Índice para el job de expiración y purga 30 días después de vencer (FR-033-05).
            var expiresKeys = Builders<OfferClaimDocument>.IndexKeys.Ascending(c => c.ExpiresAtUtc);
            var expiresOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) };
            claimsCollection.Indexes.CreateOne(
                new CreateIndexModel<OfferClaimDocument>(expiresKeys, expiresOptions));

            var receiptKeys = Builders<ClaimPushReceiptDocument>.IndexKeys.Ascending(r => r.ExpiresAtUtc);
            ClaimPushReceipts.Indexes.CreateOne(new CreateIndexModel<ClaimPushReceiptDocument>(
                receiptKeys, new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de apartados ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var viewsCollection = _database.GetCollection<OfferViewDocument>(_settings.OfferViewsCollectionName);

            // Una sola vista por usuario, oferta y día (FR-032-03, CA-032-04).
            var viewKeys = Builders<OfferViewDocument>.IndexKeys
                .Ascending(v => v.UserId)
                .Ascending(v => v.OfferId)
                .Ascending(v => v.ViewedOn);
            var viewOptions = new CreateIndexOptions { Unique = true };
            viewsCollection.Indexes.CreateOne(
                new CreateIndexModel<OfferViewDocument>(viewKeys, viewOptions));

            // Purga de vistas con 180 días de antigüedad (plan del spec 032).
            var viewTtlKeys = Builders<OfferViewDocument>.IndexKeys.Ascending(v => v.CreatedAtUtc);
            var viewTtlOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(180) };
            viewsCollection.Indexes.CreateOne(
                new CreateIndexModel<OfferViewDocument>(viewTtlKeys, viewTtlOptions));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de vistas ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Índice por propietario y fecha para el límite diario de cargas (FR-022-07)
            var mediaOwnerDateKeys = Builders<MediaAssetDocument>.IndexKeys
                .Ascending(d => d.OwnerId)
                .Ascending(d => d.CreatedAtUtc);
            MediaAssets.Indexes.CreateOne(new CreateIndexModel<MediaAssetDocument>(mediaOwnerDateKeys));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de media assets ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Índice único por token de dispositivo (FR-027-02)
            var deviceTokenKeys = Builders<DeviceDocument>.IndexKeys.Ascending(d => d.Token);
            var deviceTokenOptions = new CreateIndexOptions<DeviceDocument> { Unique = true };
            Devices.Indexes.CreateOne(new CreateIndexModel<DeviceDocument>(deviceTokenKeys, deviceTokenOptions));

            // Índice compuesto por userId + deviceId para reasignación (FR-027-06)
            var deviceUserDeviceKeys = Builders<DeviceDocument>.IndexKeys
                .Ascending(d => d.UserId)
                .Ascending(d => d.DeviceId);
            Devices.Indexes.CreateOne(new CreateIndexModel<DeviceDocument>(deviceUserDeviceKeys));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de dispositivos ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Índice único por usuario para preferencias de notificación (FR-044-01)
            var settingsUserKeys = Builders<NotificationSettingsDocument>.IndexKeys.Ascending(d => d.UserId);
            var settingsUserOptions = new CreateIndexOptions<NotificationSettingsDocument> { Unique = true };
            NotificationSettings.Indexes.CreateOne(new CreateIndexModel<NotificationSettingsDocument>(settingsUserKeys, settingsUserOptions));

            var locationKeys = Builders<NotificationLocationDocument>.IndexKeys.Geo2DSphere(d => d.Coordinates);
            NotificationLocations.Indexes.CreateOne(new CreateIndexModel<NotificationLocationDocument>(locationKeys));
            var locationExpiry = Builders<NotificationLocationDocument>.IndexKeys.Ascending(d => d.ExpiresAtUtc);
            NotificationLocations.Indexes.CreateOne(new CreateIndexModel<NotificationLocationDocument>(
                locationExpiry, new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de preferencias de notificación ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Índices para conversaciones de Sensi IA (FR-046-01)
            var conversationOwnerKeys = Builders<AiConversationDocument>.IndexKeys
                .Ascending(c => c.OwnerUserId)
                .Ascending(c => c.Status);
            AiConversations.Indexes.CreateOne(new CreateIndexModel<AiConversationDocument>(conversationOwnerKeys));

            // Índices para mensajes de Sensi IA
            var messageConvKeys = Builders<AiMessageDocument>.IndexKeys
                .Ascending(m => m.ConversationId)
                .Ascending(m => m.CreatedAtUtc);
            AiMessages.Indexes.CreateOne(new CreateIndexModel<AiMessageDocument>(messageConvKeys));

            var messageOwnerKeys = Builders<AiMessageDocument>.IndexKeys.Ascending(m => m.OwnerUserId);
            AiMessages.Indexes.CreateOne(new CreateIndexModel<AiMessageDocument>(messageOwnerKeys));

            // Índices para trazas de auditoría de Sensi IA con TTL (FR-046-15)
            var traceTtlKeys = Builders<AiTraceLogDocument>.IndexKeys.Ascending(t => t.ExpiresAtUtc);
            var traceTtlOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero };
            AiTraceLogs.Indexes.CreateOne(new CreateIndexModel<AiTraceLogDocument>(traceTtlKeys, traceTtlOptions));

            var traceConvKeys = Builders<AiTraceLogDocument>.IndexKeys
                .Ascending(t => t.ConversationId)
                .Ascending(t => t.CreatedAtUtc);
            AiTraceLogs.Indexes.CreateOne(new CreateIndexModel<AiTraceLogDocument>(traceConvKeys));

            var traceOwnerKeys = Builders<AiTraceLogDocument>.IndexKeys.Ascending(t => t.OwnerUserId);
            AiTraceLogs.Indexes.CreateOne(new CreateIndexModel<AiTraceLogDocument>(traceOwnerKeys));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: Los índices de Sensi IA ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var friendUsersKeys = Builders<FriendRequestDocument>.IndexKeys
                .Ascending(f => f.SenderUserId)
                .Ascending(f => f.TargetUserId);
            FriendRequests.Indexes.CreateOne(new CreateIndexModel<FriendRequestDocument>(friendUsersKeys));

            var friendTargetKeys = Builders<FriendRequestDocument>.IndexKeys
                .Ascending(f => f.TargetUserId)
                .Ascending(f => f.Status);
            FriendRequests.Indexes.CreateOne(new CreateIndexModel<FriendRequestDocument>(friendTargetKeys));

            var sharedOffersKeys = Builders<SharedOfferDocument>.IndexKeys
                .Ascending(s => s.SenderUserId)
                .Ascending(s => s.RecipientUserId)
                .Ascending(s => s.SharedAtUtc);
            SharedOffers.Indexes.CreateOne(new CreateIndexModel<SharedOfferDocument>(sharedOffersKeys));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: Los índices de amigos ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var likes = Builders<OfferLikeDocument>.IndexKeys;
            OfferLikes.Indexes.CreateMany([
                new CreateIndexModel<OfferLikeDocument>(likes.Ascending(l => l.UserId).Ascending(l => l.OfferId),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<OfferLikeDocument>(likes.Ascending(l => l.OfferId))
            ]);
            var reviews = Builders<StoreReviewDocument>.IndexKeys;
            StoreReviews.Indexes.CreateMany([
                new CreateIndexModel<StoreReviewDocument>(reviews.Ascending(r => r.MerchantId).Descending(r => r.CreatedAtUtc)),
                new CreateIndexModel<StoreReviewDocument>(reviews.Ascending(r => r.AuthorUserId)),
                new CreateIndexModel<StoreReviewDocument>(new BsonDocument("replies.authorUserId", 1))
            ]);

            var approvedImageKeys = Builders<MediaAssetDocument>.IndexKeys
                .Ascending(asset => asset.OwnerId).Ascending(asset => asset.SecureUrl);
            MediaAssets.Indexes.CreateOne(new CreateIndexModel<MediaAssetDocument>(approvedImageKeys));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("No se pudieron verificar los índices de actividad e imágenes ({ErrorType}).", ex.GetType().Name);
            throw;
        }
    }
}
