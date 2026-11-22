using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Enums;
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
    private readonly int _interactionRetentionDays;

    public MongoDbContext(
        IOptions<MongoDbSettings> settings,
        ILogger<MongoDbContext> logger,
        IOptions<InteractionSettings>? interactionSettings = null)
    {
        _settings = settings.Value;
        _logger = logger;
        _interactionRetentionDays = interactionSettings?.Value.RetentionDays > 0
            ? interactionSettings.Value.RetentionDays
            : 180;

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

    public IMongoCollection<SchemaMigrationDocument> SchemaMigrations =>
        _database?.GetCollection<SchemaMigrationDocument>("schema_migrations")
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<UserInterestsDocument> UserInterests =>
        _database?.GetCollection<UserInterestsDocument>(_settings.UserInterestsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferInteractionDocument> OfferInteractions =>
        _database?.GetCollection<OfferInteractionDocument>(_settings.OfferInteractionsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferLikeDocument> OfferLikes =>
        _database?.GetCollection<OfferLikeDocument>("offer_likes")
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<StoreReviewDocument> StoreReviews =>
        _database?.GetCollection<StoreReviewDocument>("store_reviews")
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<FeedCommentDocument> FeedComments =>
        _database?.GetCollection<FeedCommentDocument>(_settings.FeedCommentsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<ContentReportDocument> ContentReports =>
        _database?.GetCollection<ContentReportDocument>(_settings.ContentReportsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<OfferStatusHistoryDocument> OfferStatusHistory =>
        _database?.GetCollection<OfferStatusHistoryDocument>(_settings.OfferStatusHistoryCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<MongoDB.Bson.BsonDocument> FeedPostsRaw =>
        _database?.GetCollection<MongoDB.Bson.BsonDocument>(_settings.FeedPostsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<MongoDB.Bson.BsonDocument> OffersRaw =>
        _database?.GetCollection<MongoDB.Bson.BsonDocument>(_settings.OffersCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoCollection<MongoDB.Bson.BsonDocument> FeedCommentsRaw =>
        _database?.GetCollection<MongoDB.Bson.BsonDocument>(_settings.FeedCommentsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    public IMongoDatabase Database =>
        _database
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

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

            // Índice único de texto en español (Spec 041): stemming nativo en español sobre título, comercio, categoría y descripción.
            EnsureSpanishTextIndex(offersCollection);

            // Índice ascendente sobre TitleNormalized para coincidencia rápida por prefijo sin acentos (Spec 041, T041-06).
            var titleNormKeys = Builders<OfferDocument>.IndexKeys.Ascending(o => o.TitleNormalized);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(
                titleNormKeys,
                new CreateIndexOptions { Name = "ix_offers_title_normalized" }));

            // Índice compuesto (Category, Status, DiscountedPrice, EndsAt) para alternativas comparables (Spec 043, T043-06).
            var alternativesKeys = Builders<OfferDocument>.IndexKeys
                .Ascending(o => o.Category)
                .Ascending(o => o.Status)
                .Ascending(o => o.DiscountedPrice)
                .Ascending(o => o.EndsAt);
            offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(
                alternativesKeys,
                new CreateIndexOptions { Name = "ix_offers_category_status_price" }));
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

            // Índice único parcial para impedir más de una conversación Active por usuario bajo concurrencia
            var activeConversationUniqueKeys = Builders<AiConversationDocument>.IndexKeys
                .Ascending(c => c.OwnerUserId);
            var activeConversationUniqueOptions = new CreateIndexOptions<AiConversationDocument>
            {
                Name = "ux_ai_conversations_owner_active",
                Unique = true,
                PartialFilterExpression = Builders<AiConversationDocument>.Filter.Eq(
                    c => c.Status,
                    AiConversationStatus.Active.ToString())
            };
            AiConversations.Indexes.CreateOne(
                new CreateIndexModel<AiConversationDocument>(activeConversationUniqueKeys, activeConversationUniqueOptions));

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
            // Spec 054: índice único por userId en user_interests para lecturas y upserts O(1)
            var userInterestsKeys = Builders<UserInterestsDocument>.IndexKeys.Ascending(u => u.UserId);
            UserInterests.Indexes.CreateOne(new CreateIndexModel<UserInterestsDocument>(
                userInterestsKeys,
                new CreateIndexOptions { Name = "ux_user_interests_user_id", Unique = true }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: El índice de user_interests ya existe o el clúster aún no está disponible.");
        }

        try
        {
            var offerLikeUniqueKeys = Builders<OfferLikeDocument>.IndexKeys
                .Ascending(l => l.UserId)
                .Ascending(l => l.OfferId);
            OfferLikes.Indexes.CreateOne(new CreateIndexModel<OfferLikeDocument>(
                offerLikeUniqueKeys,
                new CreateIndexOptions { Unique = true }));

            var offerLikeOfferKeys = Builders<OfferLikeDocument>.IndexKeys
                .Ascending(l => l.OfferId)
                .Descending(l => l.CreatedAtUtc);
            OfferLikes.Indexes.CreateOne(new CreateIndexModel<OfferLikeDocument>(offerLikeOfferKeys));

            var reviewMerchantKeys = Builders<StoreReviewDocument>.IndexKeys
                .Ascending(r => r.MerchantId)
                .Descending(r => r.CreatedAtUtc);
            StoreReviews.Indexes.CreateOne(new CreateIndexModel<StoreReviewDocument>(reviewMerchantKeys));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: Los índices sociales de likes y reseñas ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Spec 055: TTL 180 días (FR-055-06), purga por seudónimo+versión (FR-055-07) y agregación diaria (FR-055-10)
            var interactionTtlKeys = Builders<OfferInteractionDocument>.IndexKeys.Ascending(i => i.OccurredAtUtc);
            OfferInteractions.Indexes.CreateOne(new CreateIndexModel<OfferInteractionDocument>(
                interactionTtlKeys,
                new CreateIndexOptions
                {
                    Name = "ttl_offer_interactions_occurred_at",
                    ExpireAfter = TimeSpan.FromDays(_interactionRetentionDays)
                }));

            var interactionPurgeKeys = Builders<OfferInteractionDocument>.IndexKeys
                .Ascending(i => i.PseudoUserId)
                .Ascending(i => i.KeyVersion);
            OfferInteractions.Indexes.CreateOne(new CreateIndexModel<OfferInteractionDocument>(
                interactionPurgeKeys,
                new CreateIndexOptions { Name = "ix_offer_interactions_pseudo_key" }));

            var interactionQualityKeys = Builders<OfferInteractionDocument>.IndexKeys
                .Ascending(i => i.RankingVersion)
                .Ascending(i => i.Type)
                .Ascending(i => i.OccurredAtUtc);
            OfferInteractions.Indexes.CreateOne(new CreateIndexModel<OfferInteractionDocument>(
                interactionQualityKeys,
                new CreateIndexOptions { Name = "ix_offer_interactions_quality" }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: Los índices de offer_interactions ya existen o el clúster aún no está disponible.");
        }

        try
        {
            // Spec 025: comentarios de publicaciones paginados cronológicamente
            var feedCommentKeys = Builders<FeedCommentDocument>.IndexKeys
                .Ascending(c => c.PostId)
                .Ascending(c => c.CreatedAtUtc);
            FeedComments.Indexes.CreateOne(new CreateIndexModel<FeedCommentDocument>(
                feedCommentKeys,
                new CreateIndexOptions { Name = "ix_feed_comments_post_created_at" }));

            // Spec 040: deduplicación única por ventana y conteo rápido por contenido
            var reportDedupKeys = Builders<ContentReportDocument>.IndexKeys
                .Ascending(r => r.ReporterUserId)
                .Ascending(r => r.TargetType)
                .Ascending(r => r.TargetId)
                .Ascending(r => r.WindowBucket);
            ContentReports.Indexes.CreateOne(new CreateIndexModel<ContentReportDocument>(
                reportDedupKeys,
                new CreateIndexOptions { Name = "ux_content_reports_dedup", Unique = true }));

            var reportTargetKeys = Builders<ContentReportDocument>.IndexKeys
                .Ascending(r => r.TargetType)
                .Ascending(r => r.TargetId)
                .Descending(r => r.CreatedAtUtc);
            ContentReports.Indexes.CreateOne(new CreateIndexModel<ContentReportDocument>(
                reportTargetKeys,
                new CreateIndexOptions { Name = "ix_content_reports_target_window" }));

            // Spec 036: historial auditable de transiciones de ofertas
            var offerHistoryKeys = Builders<OfferStatusHistoryDocument>.IndexKeys
                .Ascending(h => h.OfferId)
                .Ascending(h => h.OccurredAtUtc);
            OfferStatusHistory.Indexes.CreateOne(new CreateIndexModel<OfferStatusHistoryDocument>(
                offerHistoryKeys,
                new CreateIndexOptions { Name = "ix_offer_status_history_offer" }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Nota: Los índices de comentarios, reportes o historial de ofertas ya existen o el clúster aún no está disponible.");
        }
    }

    private static void EnsureSpanishTextIndex(IMongoCollection<OfferDocument> offersCollection)
    {
        const string SpanishTextIndexName = "txt_offers_spanish";

        List<MongoDB.Bson.BsonDocument> existing = offersCollection.Indexes.List().ToList();
        bool targetExists = false;

        foreach (MongoDB.Bson.BsonDocument index in existing)
        {
            string? name = index.TryGetValue("name", out MongoDB.Bson.BsonValue nameVal) && nameVal.IsString
                ? nameVal.AsString
                : null;
            bool isTextIndex = index.TryGetValue("key", out MongoDB.Bson.BsonValue keyVal) &&
                               keyVal.IsBsonDocument &&
                               keyVal.AsBsonDocument.Elements.Any(e => e.Value.IsString && e.Value.AsString == "text")
                               || index.Contains("weights");

            if (!isTextIndex)
            {
                continue;
            }

            if (string.Equals(name, SpanishTextIndexName, StringComparison.Ordinal))
            {
                targetExists = true;
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                offersCollection.Indexes.DropOne(name);
            }
        }

        if (targetExists)
        {
            return;
        }

        var textIndexKeys = Builders<OfferDocument>.IndexKeys
            .Text(o => o.Title)
            .Text(o => o.MerchantName)
            .Text(o => o.CategorySearchText)
            .Text(o => o.Category)
            .Text(o => o.Description);

        var textOptions = new CreateIndexOptions<OfferDocument>
        {
            Name = SpanishTextIndexName,
            DefaultLanguage = "spanish",
            Weights = new MongoDB.Bson.BsonDocument
            {
                { "title", 10 },
                { "merchantName", 6 },
                { "categorySearchText", 5 },
                { "category", 4 },
                { "description", 2 }
            }
        };

        offersCollection.Indexes.CreateOne(new CreateIndexModel<OfferDocument>(textIndexKeys, textOptions));
    }
}
