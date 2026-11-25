using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación de repositorio para Comercio con persistencia directa en MongoDB Atlas.
/// Soporta consultas geoespaciales 2dsphere para búsqueda de proximidad (SE-008).
/// </summary>
public sealed class MongoComercioRepository : IComercioRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoComercioRepository> _logger;

    public MongoComercioRepository(
        MongoDbContext context,
        ILogger<MongoComercioRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Comercio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ComercioDocument>.Filter.Eq(c => c.Id, id);
        var document = await _context.Comercios.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<Comercio?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ComercioDocument>.Filter.Eq(c => c.UsuarioId, usuarioId);
        var document = await _context.Comercios.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<bool> ExistsByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ComercioDocument>.Filter.Eq(c => c.UsuarioId, usuarioId);
        var count = await _context.Comercios.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return count > 0;
    }

    public async Task<(IReadOnlyList<Comercio> Items, int TotalCount)> GetNearbyAsync(
        double latitud,
        double longitud,
        int radioMetros,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        BsonDocument[] pipeline =
        [
            new("$geoNear", new BsonDocument
            {
                { "near", new BsonDocument { { "type", "Point" }, { "coordinates", new BsonArray { longitud, latitud } } } },
                { "key", "ubicacion" },
                { "distanceField", "distanceMeters" },
                { "maxDistance", radioMetros },
                { "spherical", true },
                { "query", new BsonDocument("activo", true) }
            }),
            new("$facet", new BsonDocument
            {
                { "total", new BsonArray { new BsonDocument("$count", "count") } },
                { "items", new BsonArray
                    {
                        new BsonDocument("$sort", new BsonDocument { { "distanceMeters", 1 }, { "_id", 1 } }),
                        new BsonDocument("$skip", (page - 1L) * pageSize),
                        new BsonDocument("$limit", pageSize),
                        new BsonDocument("$unset", "distanceMeters")
                    }
                }
            })
        ];

        var result = await _context.Comercios.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null)
        {
            return ([], 0);
        }

        var totals = result["total"].AsBsonArray;
        var total = totals.Count == 0 ? 0 : totals[0]["count"].ToInt32();
        var items = result["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<ComercioDocument>(item.AsBsonDocument).ToDomain())
            .ToList();
        return (items, total);
    }

    public async Task AddAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        var document = ComercioDocument.FromDomain(comercio);
        await _context.Comercios.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        var document = ComercioDocument.FromDomain(comercio);
        var filter = Builders<ComercioDocument>.Filter.Eq(c => c.Id, comercio.Id);
        await _context.Comercios.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    public async Task DeleteByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ComercioDocument>.Filter.Eq(c => c.UsuarioId, usuarioId);
        await _context.Comercios.DeleteManyAsync(filter, cancellationToken);
    }
}
