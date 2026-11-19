using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Xunit;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Spec 020: persistencia de sanciones contra un MongoDB real (Principio V: sin dobles que simulen el motor).
/// Se ejecuta cuando <c>MONGO_CONNECTION_STRING</c> está definida (el job de integración de CI la define) y usa
/// una base de datos efímera por prueba, que elimina al terminar.
/// </summary>
public sealed class MongoSanctionRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_sanctions_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoSanctionRepository? _repository;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _repository = new MongoSanctionRepository(_context);
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }
    }

    private bool Enabled => _repository is not null;

    private static Sanction Automatic(Guid userId, DateTime startsAt, int? hours, Guid? mediaId = null) =>
        Sanction.CreateAutomatic(
            userId,
            "Contenido rechazado",
            new SanctionEvidence(mediaId, "Rejected", ["weapons"], new Dictionary<string, double> { ["weapons"] = 0.91 }, "weapons>=0.60", "UserSuspended"),
            startsAt,
            hours).Value;

    [Fact]
    public async Task AddAndGet_RoundTripsEvidenceDatesAndPermanence()
    {
        if (!Enabled) return;
        var mediaId = Guid.NewGuid();
        var original = Automatic(Guid.NewGuid(), Now, 24, mediaId);

        await _repository!.AddAsync(original);
        var loaded = await _repository.GetByIdAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.UserId.Should().Be(original.UserId);
        loaded.StartsAtUtc.Should().Be(Now);
        loaded.EndsAtUtc.Should().Be(Now.AddHours(24));
        loaded.Evidence.MediaId.Should().Be(mediaId);
        loaded.Evidence.Categories.Should().Equal("weapons");
        loaded.Evidence.Scores["weapons"].Should().Be(0.91);
        loaded.Type.Should().Be(SanctionType.Automatic);
        loaded.AppliedBy.Should().Be(Sanction.SystemActor);
    }

    [Fact]
    public async Task Permanent_RoundTripsWithNullEnd()
    {
        if (!Enabled) return;
        var permanent = Automatic(Guid.NewGuid(), Now, null);

        await _repository!.AddAsync(permanent);

        (await _repository.GetByIdAsync(permanent.Id))!.IsPermanent.Should().BeTrue();
    }

    [Fact]
    public async Task Update_PersistsReversalAndKeepsHistory()
    {
        if (!Enabled) return;
        var sanction = Automatic(Guid.NewGuid(), Now, 24);
        await _repository!.AddAsync(sanction);
        var admin = Guid.NewGuid();
        sanction.Revert(admin, "Falso positivo", Now.AddHours(1));

        await _repository.UpdateAsync(sanction);
        var loaded = await _repository.GetByIdAsync(sanction.Id);

        loaded!.GetState(Now.AddHours(2)).Should().Be(SanctionState.Reverted);
        loaded.RevertedBy.Should().Be(admin);
        loaded.RevertReason.Should().Be("Falso positivo");
        loaded.Evidence.Decision.Should().Be("Rejected");
    }

    [Fact]
    public async Task GetByMediaId_FindsTheSanctionOfThatAsset()
    {
        if (!Enabled) return;
        var mediaId = Guid.NewGuid();
        var sanction = Automatic(Guid.NewGuid(), Now, 24, mediaId);
        await _repository!.AddAsync(sanction);
        await _repository.AddAsync(Automatic(Guid.NewGuid(), Now, 24, Guid.NewGuid()));

        var found = await _repository.GetByMediaIdAsync(mediaId);

        found!.Id.Should().Be(sanction.Id);
        (await _repository.GetByMediaIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task UniqueIndex_PreventsTwoSanctionsForTheSameAsset_ButAllowsManyWithoutAsset()
    {
        if (!Enabled) return;
        var mediaId = Guid.NewGuid();
        await _repository!.AddAsync(Automatic(Guid.NewGuid(), Now, 24, mediaId));

        var duplicate = () => _repository.AddAsync(Automatic(Guid.NewGuid(), Now, 24, mediaId));

        await duplicate.Should().ThrowAsync<MongoWriteException>();

        var admin = Guid.NewGuid();
        await _repository.AddAsync(Sanction.CreateManual(Guid.NewGuid(), admin, "a", Now, 24).Value);
        await _repository.AddAsync(Sanction.CreateManual(Guid.NewGuid(), admin, "b", Now, 24).Value);
    }

    [Fact]
    public async Task GetActiveByUser_ExcludesExpiredAndRevertedSanctions()
    {
        if (!Enabled) return;
        var userId = Guid.NewGuid();
        var active = Automatic(userId, Now, 24);
        var permanent = Automatic(userId, Now.AddHours(-1), null);
        var expired = Automatic(userId, Now.AddDays(-5), 24);
        var reverted = Automatic(userId, Now, 24);
        reverted.Revert(Guid.NewGuid(), "x", Now).IsSuccess.Should().BeTrue();
        foreach (var s in new[] { active, permanent, expired, reverted })
        {
            await _repository!.AddAsync(s);
        }

        var result = await _repository!.GetActiveByUserAsync(userId, Now.AddHours(1));

        result.Select(s => s.Id).Should().BeEquivalentTo([active.Id, permanent.Id]);
    }

    [Fact]
    public async Task CountNotRevertedSince_CountsOnlyRecentNonRevertedSanctionsOfThatUser()
    {
        if (!Enabled) return;
        var userId = Guid.NewGuid();
        await _repository!.AddAsync(Automatic(userId, Now.AddDays(-10), 24));
        await _repository.AddAsync(Automatic(userId, Now.AddDays(-100), 24));
        var reverted = Automatic(userId, Now.AddDays(-5), 24);
        reverted.Revert(Guid.NewGuid(), "x", Now.AddDays(-5).AddHours(1)).IsSuccess.Should().BeTrue();
        await _repository.AddAsync(reverted);
        await _repository.AddAsync(Automatic(Guid.NewGuid(), Now.AddDays(-1), 24));

        int count = await _repository.CountNotRevertedSinceAsync(userId, Now.AddDays(-90));

        count.Should().Be(1);
    }

    [Fact]
    public async Task List_FiltersByUserAndStateAndPaginatesNewestFirst()
    {
        if (!Enabled) return;
        var userId = Guid.NewGuid();
        var expired = Automatic(userId, Now.AddDays(-9), 24);
        var reverted = Automatic(userId, Now.AddDays(-3), 24);
        reverted.Revert(Guid.NewGuid(), "x", Now.AddDays(-3).AddHours(1)).IsSuccess.Should().BeTrue();
        var active = Automatic(userId, Now.AddHours(-2), 24);
        var other = Automatic(Guid.NewGuid(), Now.AddHours(-1), 24);
        foreach (var s in new[] { expired, reverted, active, other })
        {
            await _repository!.AddAsync(s);
        }

        var all = await _repository!.ListAsync(userId, null, Now, 1, 10);
        var activeOnly = await _repository.ListAsync(userId, SanctionState.Active, Now, 1, 10);
        var expiredOnly = await _repository.ListAsync(userId, SanctionState.Expired, Now, 1, 10);
        var revertedOnly = await _repository.ListAsync(userId, SanctionState.Reverted, Now, 1, 10);
        var secondPage = await _repository.ListAsync(userId, null, Now, 2, 2);

        all.TotalCount.Should().Be(3);
        all.Items.Select(s => s.Id).Should().Equal(active.Id, reverted.Id, expired.Id);
        activeOnly.Items.Select(s => s.Id).Should().Equal(active.Id);
        expiredOnly.Items.Select(s => s.Id).Should().Equal(expired.Id);
        revertedOnly.Items.Select(s => s.Id).Should().Equal(reverted.Id);
        secondPage.Items.Should().ContainSingle().Which.Id.Should().Be(expired.Id);
        secondPage.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Indexes_AreDeclaredForEveryQuery()
    {
        if (!Enabled) return;
        await _repository!.AddAsync(Automatic(Guid.NewGuid(), Now, 24));

        var indexes = await (await _context!.Sanctions.Indexes.ListAsync()).ToListAsync();
        var names = indexes.Select(i => i["name"].AsString).ToList();

        names.Should().Contain(n => n.StartsWith("userId_1_startsAtUtc_-1"));
        names.Should().Contain("startsAtUtc_-1");
        names.Should().Contain("evidence.mediaId_1");
    }

    [Fact]
    public void UserDocument_RoundTripsSuspension_WithoutRotatingTheStamp()
    {
        var user = User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;
        var sanctionId = Guid.NewGuid();
        user.Suspend(sanctionId, Now.AddHours(24), Now);
        var stampAfterSuspend = user.SecurityStamp;

        var restored = UserDocument.FromDomain(user).ToDomain();

        restored.Suspension.Should().NotBeNull();
        restored.Suspension!.SanctionId.Should().Be(sanctionId);
        restored.Suspension.EndsAtUtc.Should().Be(Now.AddHours(24));
        restored.SecurityStamp.Should().Be(stampAfterSuspend);
        restored.IsSuspended(Now).Should().BeTrue();
    }

    [Fact]
    public void UserDocument_PermanentBan_RoundTripsWithNullEnd()
    {
        var user = User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;
        user.Suspend(Guid.NewGuid(), null, Now);

        var restored = UserDocument.FromDomain(user).ToDomain();

        restored.Suspension!.EndsAtUtc.Should().BeNull();
        restored.IsSuspended(Now.AddYears(20)).Should().BeTrue();
    }

    [Fact]
    public void UserDocument_WithoutSuspension_StaysUnsuspended()
    {
        var user = User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;

        UserDocument.FromDomain(user).ToDomain().Suspension.Should().BeNull();
    }
}
