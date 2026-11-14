using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo;

/// <summary>
/// Contexto de conexión hacia MongoDB Atlas.
/// </summary>
public sealed class MongoDbContext
{
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoDbContext> _logger;
    private readonly MongoDbSettings _settings;

    public MongoDbContext(
        IOptions<MongoDbSettings> settings,
        ILogger<MongoDbContext> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            _logger.LogWarning("MongoDB ConnectionString no está configurada.");
            _database = null;
        }
        else
        {
            var client = new MongoClient(_settings.ConnectionString);
            _database = client.GetDatabase(_settings.DatabaseName);
        }

        ConfigureIndexes();
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

    public IMongoCollection<OfferViewDocument> OfferViews =>
        _database?.GetCollection<OfferViewDocument>(_settings.OfferViewsCollectionName)
        ?? throw new InvalidOperationException("La conexión a MongoDB Atlas no está configurada. Configure 'MongoDbSettings:ConnectionString'.");

    private void ConfigureIndexes()
    {
        if (_database is null) return;

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
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nota: Los índices de ofertas ya existen o el clúster aún no está disponible.");
        }

        try
        {
            var claimsCollection = _database.GetCollection<OfferClaimDocument>(_settings.OfferClaimsCollectionName);

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
    }
}
