using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas contra MongoDB real (Testcontainers) para verificar los índices únicos de usuarios (<c>T001-04</c>)
/// y de comercios más <c>2dsphere</c> con plan <c>explain</c> (<c>T005-05</c>).
/// </summary>
public sealed class MongoUserAndComercioRepositoryTests : IAsyncLifetime
{
    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_b1_mongo_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoUserRepository _users = null!;
    private MongoComercioRepository _comercios = null!;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _connectionString = _envConn;
        }
        else
        {
            _container = new MongoDbBuilder("mongo:7.0").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);
        _users = new MongoUserRepository(_context, NullLogger<MongoUserRepository>.Instance);
        _comercios = new MongoComercioRepository(_context, NullLogger<MongoComercioRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Users_HasUniqueEmailIndex_AndRejectsDuplicateCorreo()
    {
        List<BsonDocument> indexes = await (await _context.Users.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i =>
            i.Contains("unique") && i["unique"].AsBoolean &&
            i["key"].AsBsonDocument.Contains("correo"));

        var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        var u1 = User.Create("Ana Uno", "ana.unica@sensus.app", "hash-1", true, now).Value;
        var u2 = User.Create("Ana Dos", "ana.unica@sensus.app", "hash-2", true, now).Value;

        await _users.AddAsync(u1);
        Func<Task> act = async () => await _users.AddAsync(u2);

        var ex = await act.Should().ThrowAsync<MongoWriteException>();
        ex.Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
    }

    [Fact]
    public async Task Comercios_HasUniqueUsuarioIndex_AndUses2dSphereInExplain()
    {
        List<BsonDocument> indexes = await (await _context.Comercios.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i =>
            i.Contains("unique") && i["unique"].AsBoolean &&
            i["key"].AsBsonDocument.Contains("usuarioId"));
        indexes.Should().Contain(i =>
            i["key"].AsBsonDocument.Contains("ubicacion") &&
            i["key"]["ubicacion"].AsString == "2dsphere");

        var ownerId = Guid.NewGuid();
        var c1 = Comercio.Create(ownerId, "Café Centro", "Av. Reforma 1", "06600", 19.4326, -99.1332).Value;
        var c2 = Comercio.Create(ownerId, "Café Duplicado", "Av. Reforma 2", "06600", 19.4327, -99.1333).Value;

        await _comercios.AddAsync(c1);
        Func<Task> dupAct = async () => await _comercios.AddAsync(c2);
        var ex = await dupAct.Should().ThrowAsync<MongoWriteException>();
        ex.Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);

        var nearby = await _comercios.GetNearbyAsync(19.4326, -99.1332, 2000, 1, 20);
        nearby.TotalCount.Should().Be(1);
        nearby.Items.Should().ContainSingle(x => x.Id == c1.Id);

        var explainCmd = new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "aggregate", _context.Comercios.CollectionNamespace.CollectionName },
                    { "pipeline", new BsonArray
                        {
                            new BsonDocument("$geoNear", new BsonDocument
                            {
                                { "near", new BsonDocument { { "type", "Point" }, { "coordinates", new BsonArray { -99.1332, 19.4326 } } } },
                                { "key", "ubicacion" },
                                { "distanceField", "distanceMeters" },
                                { "maxDistance", 2000 },
                                { "spherical", true },
                                { "query", new BsonDocument("activo", true) }
                            })
                        }
                    },
                    { "cursor", new BsonDocument() }
                }
            },
            { "verbosity", "queryPlanner" }
        };

        var explain = await _context.Database.RunCommandAsync<BsonDocument>(explainCmd);
        var explainJson = explain.ToJson();
        explainJson.Should().Contain("GEO_NEAR_2DSPHERE");
        explainJson.Should().NotContain("\"stage\" : \"COLLSCAN\"");
    }
}
