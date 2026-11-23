using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;
using Xunit;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Spec 027 (T027-08 / FR-027-06): Pruebas de integración de MongoDeviceRepository contra MongoDB real (Testcontainers).
/// Valida la regresión de reasignación de token entre usuarios, actualización y desactivación de tokens de dispositivo.
/// </summary>
public sealed class MongoDeviceRepositoryTests : IAsyncLifetime
{
    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_devices_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoDeviceRepository _repository = null!;

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
            Options.Create(new MongoDbSettings
            {
                ConnectionString = _connectionString,
                DatabaseName = _databaseName,
                DevicesCollectionName = "devices"
            }),
            NullLogger<MongoDbContext>.Instance);

        _repository = new MongoDeviceRepository(_context, NullLogger<MongoDeviceRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }
        else if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReassignToken_BetweenDifferentUsers_UnbindsFromOldUserAndBindsToNewUser()
    {
        // Arrange: Usuario A registra token en dispositivo 1
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var sharedToken = "fcm_token_shared_" + Guid.NewGuid().ToString("N");

        var deviceA = Device.Create(userA, "device-phone-a", "Android", sharedToken, DateTime.UtcNow);
        await _repository.AddAsync(deviceA);

        // Verificar persistencia de Usuario A
        var savedA = await _repository.GetByTokenAsync(sharedToken);
        savedA.Should().NotBeNull();
        savedA!.UserId.Should().Be(userA);

        // Act: Reasignación — Usuario B registra el mismo token
        // Flujo del comando: localiza binding previo y ejecuta TryRemoveTokenBindingAsync
        var existingBinding = await _repository.GetByTokenAsync(sharedToken);
        existingBinding.Should().NotBeNull();
        existingBinding!.UserId.Should().Be(userA);

        bool removed = await _repository.TryRemoveTokenBindingAsync(
            existingBinding.Id, existingBinding.UserId, sharedToken);
        removed.Should().BeTrue();

        var deviceB = Device.Create(userB, "device-tablet-b", "Android", sharedToken, DateTime.UtcNow);
        await _repository.AddAsync(deviceB);

        // Assert: El token ahora pertenece exclusivamente a Usuario B
        var currentTokenOwner = await _repository.GetByTokenAsync(sharedToken);
        currentTokenOwner.Should().NotBeNull();
        currentTokenOwner!.UserId.Should().Be(userB);
        currentTokenOwner.DeviceId.Should().Be("device-tablet-b");

        // Usuario A ya no tiene el token
        var userADevices = await _repository.GetActiveByUserAsync(userA);
        userADevices.Should().NotContain(d => d.Token == sharedToken);

        // Usuario B tiene el dispositivo activo con el token
        var userBDevices = await _repository.GetActiveByUserAsync(userB);
        userBDevices.Should().ContainSingle(d => d.Token == sharedToken && d.IsActive);
    }

    [Fact]
    public async Task UpdateToken_SameUserAndDevice_UpdatesTokenAndMaintainsSingleRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialToken = "fcm_initial_" + Guid.NewGuid().ToString("N");
        var updatedToken = "fcm_updated_" + Guid.NewGuid().ToString("N");

        var device = Device.Create(userId, "device-pixel", "Android", initialToken, DateTime.UtcNow);
        await _repository.AddAsync(device);

        // Act
        device.UpdateToken(updatedToken);
        device.Activate();
        await _repository.UpdateAsync(device);

        // Assert
        var oldLookup = await _repository.GetByTokenAsync(initialToken);
        oldLookup.Should().BeNull();

        var newLookup = await _repository.GetByTokenAsync(updatedToken);
        newLookup.Should().NotBeNull();
        newLookup!.UserId.Should().Be(userId);
        newLookup.Token.Should().Be(updatedToken);
    }

    [Fact]
    public async Task DeactivateByToken_DeactivatesDeviceInDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "fcm_token_to_deactivate_" + Guid.NewGuid().ToString("N");

        var device = Device.Create(userId, "device-samsung", "Android", token, DateTime.UtcNow);
        await _repository.AddAsync(device);

        // Act
        await _repository.DeactivateByTokenAsync(device.Id, token);

        // Assert: Sigue existiendo por ID o token, pero IsActive es false
        var fetched = await _repository.GetByIdAsync(device.Id);
        fetched.Should().NotBeNull();
        fetched!.IsActive.Should().BeFalse();

        var activeList = await _repository.GetActiveByUserAsync(userId);
        activeList.Should().BeEmpty();
    }
}
