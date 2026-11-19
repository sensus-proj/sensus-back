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

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de persistencia y mapeo de Sensi IA contra MongoDB real (Spec 046, T046-21, FR-046-01, FR-046-15).
/// Las pruebas de round-trip de documentos corren siempre; las de base de datos real se ejecutan cuando
/// <c>MONGO_CONNECTION_STRING</c> está definida (como en el job <c>integration-tests</c> de CI).
/// </summary>
public sealed class MongoAiRepositoriesTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_ai_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoAiConversationRepository? _conversations;
    private MongoAiMessageRepository? _messages;
    private MongoAiTraceLogRepository? _traces;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _conversations = new MongoAiConversationRepository(_context, NullLogger<MongoAiConversationRepository>.Instance);
            _messages = new MongoAiMessageRepository(_context, NullLogger<MongoAiMessageRepository>.Instance);
            _traces = new MongoAiTraceLogRepository(_context, NullLogger<MongoAiTraceLogRepository>.Instance);
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

    private bool Enabled => _context is not null;

    [Fact]
    public void AiConversationDocument_RoundTripsAllFieldsAndArchivedStatus()
    {
        var userId = Guid.NewGuid();
        var conversation = AiConversation.Create(
            userId,
            RoleType.Comerciante,
            "gpt-4o",
            AiRoutingTier.Reasoning,
            "1.0.0",
            "merchant_inventory",
            Now).Value;
        conversation.IncrementMessageCount(Now.AddMinutes(1));
        conversation.Archive(Now.AddMinutes(5));

        var restored = AiConversationDocument.FromDomain(conversation).ToDomain();

        restored.Id.Should().Be(conversation.Id);
        restored.OwnerUserId.Should().Be(userId);
        restored.OwnerRole.Should().Be(RoleType.Comerciante);
        restored.Status.Should().Be(AiConversationStatus.Archived);
        restored.AssignedModel.Should().Be("gpt-4o");
        restored.RoutingTier.Should().Be(AiRoutingTier.Reasoning);
        restored.MarkdownVersion.Should().Be("1.0.0");
        restored.LastScreenContext.Should().Be("merchant_inventory");
        restored.MessageCount.Should().Be(1);
    }

    [Fact]
    public void AiMessageDocument_RoundTripsCitedOffersToolsAndDegradation()
    {
        var pkg = RecommendedPackage.Create(
            120m,
            [new RecommendedPackageItem("offer-1", "Pan dulce", "m-1", "Panadería", 45m, 60m, 4, true, 0.8)]).Value;
        var msg = AiMessage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AiMessageRole.Assistant,
            "Te recomiendo el pan dulce.",
            true,
            "Cuota agotada",
            pkg,
            ["offer-1"],
            ["search_offers"],
            Now).Value;

        var restored = AiMessageDocument.FromDomain(msg).ToDomain();

        restored.Id.Should().Be(msg.Id);
        restored.Role.Should().Be(AiMessageRole.Assistant);
        restored.Degraded.Should().BeTrue();
        restored.DegradationReason.Should().Be("Cuota agotada");
        restored.OffersCitedIds.Should().Equal("offer-1");
        restored.ToolsInvoked.Should().Equal("search_offers");
        restored.RecommendedPackage.Should().NotBeNull();
        restored.RecommendedPackage!.TotalPriceMxn.Should().Be(45m);
        restored.RecommendedPackage.Items.Should().ContainSingle(i => i.OfferId == "offer-1");
    }

    [Fact]
    public void AiTraceLogDocument_RoundTripsTokensCostAndTtlExpiry()
    {
        var trace = AiTraceLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-trace-1",
            "1.0.0",
            "sha256-hash",
            AiRoutingTier.Fast,
            "gpt-4o-mini",
            "Hola [EMAIL_REDACTED]",
            "Respuesta redactada",
            ["search_offers"],
            1200,
            900,
            80,
            420,
            0.0012m,
            Now,
            TimeSpan.FromDays(30)).Value;

        var restored = AiTraceLogDocument.FromDomain(trace).ToDomain();

        restored.Id.Should().Be(trace.Id);
        restored.CorrelationId.Should().Be("corr-trace-1");
        restored.RoutingTier.Should().Be(AiRoutingTier.Fast);
        restored.PromptTokens.Should().Be(1200);
        restored.CachedTokens.Should().Be(900);
        restored.CompletionTokens.Should().Be(80);
        restored.TotalLatencyMs.Should().Be(420);
        restored.EstimatedCostUsd.Should().Be(0.0012m);
        restored.ExpiresAtUtc.Should().Be(Now.AddDays(30));
    }

    [Fact]
    public async Task Conversation_AddGetActiveAndArchive_WorksAgainstRealMongo()
    {
        if (!Enabled) return;

        var userId = Guid.NewGuid();
        var first = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", "home", Now).Value;
        await _conversations!.AddAsync(first);

        var loadedActive = await _conversations.GetActiveByUserIdAsync(userId);
        loadedActive.Should().NotBeNull();
        loadedActive!.Id.Should().Be(first.Id);

        first.Archive(Now.AddMinutes(5));
        await _conversations.UpdateAsync(first);

        var second = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", "map", Now.AddMinutes(6)).Value;
        await _conversations.AddAsync(second);

        var currentActive = await _conversations.GetActiveByUserIdAsync(userId);
        currentActive.Should().NotBeNull();
        currentActive!.Id.Should().Be(second.Id);
        (await _conversations.GetByIdAsync(first.Id))!.Status.Should().Be(AiConversationStatus.Archived);
    }

    [Fact]
    public async Task Messages_PagedAndRecent_ReturnChronologicalOrderAndCount()
    {
        if (!Enabled) return;

        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        for (int i = 0; i < 4; i++)
        {
            var msg = AiMessage.Create(
                convId,
                userId,
                i % 2 == 0 ? AiMessageRole.User : AiMessageRole.Assistant,
                $"Mensaje {i}",
                false,
                null,
                null,
                null,
                null,
                Now.AddSeconds(i)).Value;
            await _messages!.AddAsync(msg);
        }

        int count = await _messages!.CountByConversationIdAsync(convId);
        var page1 = await _messages.GetPagedByConversationIdAsync(convId, page: 1, pageSize: 2);
        var recent = await _messages.GetRecentByConversationIdAsync(convId, limit: 3);

        count.Should().Be(4);
        page1.Select(m => m.RedactedContent).Should().Equal("Mensaje 2", "Mensaje 3");
        recent.Select(m => m.RedactedContent).Should().Equal("Mensaje 1", "Mensaje 2", "Mensaje 3");
    }

    [Fact]
    public async Task DeleteConversation_CascadesMessagesAndTraceLogs_WithoutAffectingOthers()
    {
        if (!Enabled) return;

        var userId = Guid.NewGuid();
        var targetConv = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, Now).Value;
        var keepConv = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, Now.AddMinutes(1)).Value;
        await _conversations!.AddAsync(targetConv);
        await _conversations.AddAsync(keepConv);

        await _messages!.AddAsync(AiMessage.Create(targetConv.Id, userId, AiMessageRole.User, "Borrar", false, null, null, null, null, Now).Value);
        await _messages.AddAsync(AiMessage.Create(keepConv.Id, userId, AiMessageRole.User, "Conservar", false, null, null, null, null, Now).Value);

        await _traces!.AddAsync(AiTraceLog.Create(targetConv.Id, userId, "c1", "1.0.0", "h1", AiRoutingTier.Fast, "gpt-4o-mini", "p", "r", null, 1, 0, 1, 10, 0m, Now, TimeSpan.FromDays(30)).Value);
        await _traces.AddAsync(AiTraceLog.Create(keepConv.Id, userId, "c2", "1.0.0", "h1", AiRoutingTier.Fast, "gpt-4o-mini", "p", "r", null, 1, 0, 1, 10, 0m, Now, TimeSpan.FromDays(30)).Value);

        await _conversations.DeleteAsync(targetConv.Id);

        (await _conversations.GetByIdAsync(targetConv.Id)).Should().BeNull();
        (await _messages.CountByConversationIdAsync(targetConv.Id)).Should().Be(0);
        long targetTraces = await _context!.AiTraceLogs.CountDocumentsAsync(t => t.ConversationId == targetConv.Id);
        targetTraces.Should().Be(0);

        (await _conversations.GetByIdAsync(keepConv.Id)).Should().NotBeNull();
        (await _messages.CountByConversationIdAsync(keepConv.Id)).Should().Be(1);
        long keepTraces = await _context.AiTraceLogs.CountDocumentsAsync(t => t.ConversationId == keepConv.Id);
        keepTraces.Should().Be(1);
    }

    [Fact]
    public async Task PurgeAllByUserId_DeletesAllThreeCollectionsForUserOnly()
    {
        if (!Enabled) return;

        var targetUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var targetConv = AiConversation.Create(targetUser, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, Now).Value;
        var otherConv = AiConversation.Create(otherUser, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, Now).Value;
        await _conversations!.AddAsync(targetConv);
        await _conversations.AddAsync(otherConv);

        await _messages!.AddAsync(AiMessage.Create(targetConv.Id, targetUser, AiMessageRole.User, "M1", false, null, null, null, null, Now).Value);
        await _messages.AddAsync(AiMessage.Create(otherConv.Id, otherUser, AiMessageRole.User, "M2", false, null, null, null, null, Now).Value);

        await _traces!.AddAsync(AiTraceLog.Create(targetConv.Id, targetUser, "c1", "1.0.0", "h", AiRoutingTier.Fast, "gpt-4o-mini", "p", "r", null, 1, 0, 1, 5, 0m, Now, TimeSpan.FromDays(30)).Value);
        await _traces.AddAsync(AiTraceLog.Create(otherConv.Id, otherUser, "c2", "1.0.0", "h", AiRoutingTier.Fast, "gpt-4o-mini", "p", "r", null, 1, 0, 1, 5, 0m, Now, TimeSpan.FromDays(30)).Value);

        await _conversations.PurgeAllByUserIdAsync(targetUser);

        (await _conversations.GetByIdAsync(targetConv.Id)).Should().BeNull();
        (await _messages.CountByConversationIdAsync(targetConv.Id)).Should().Be(0);
        (await _context!.AiTraceLogs.CountDocumentsAsync(t => t.OwnerUserId == targetUser)).Should().Be(0);

        (await _conversations.GetByIdAsync(otherConv.Id)).Should().NotBeNull();
        (await _messages.CountByConversationIdAsync(otherConv.Id)).Should().Be(1);
        (await _context.AiTraceLogs.CountDocumentsAsync(t => t.OwnerUserId == otherUser)).Should().Be(1);
    }

    [Fact]
    public async Task Indexes_IncludeConversationMessageAndTraceTtlIndex()
    {
        if (!Enabled) return;

        var convIndexes = await (await _context!.AiConversations.Indexes.ListAsync()).ToListAsync();
        var msgIndexes = await (await _context.AiMessages.Indexes.ListAsync()).ToListAsync();
        var traceIndexes = await (await _context.AiTraceLogs.Indexes.ListAsync()).ToListAsync();

        convIndexes.Select(i => i["name"].AsString).Should().Contain("ownerUserId_1_status_1");
        convIndexes.Should().Contain(i =>
            i["name"].AsString == "ux_ai_conversations_owner_active" &&
            i.Contains("unique") &&
            i["unique"].AsBoolean);
        msgIndexes.Select(i => i["name"].AsString).Should().Contain(["conversationId_1_createdAtUtc_1", "ownerUserId_1"]);
        traceIndexes.Should().Contain(i =>
            i["name"].AsString == "expiresAtUtc_1" &&
            i.Contains("expireAfterSeconds") &&
            i["expireAfterSeconds"].ToInt64() == 0);
    }

    [Fact]
    public async Task Conversation_PartialUniqueIndex_RejectsSecondActiveConversationForSameUser()
    {
        if (!Enabled) return;

        var userId = Guid.NewGuid();
        var first = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", "home", Now).Value;
        var second = AiConversation.Create(userId, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", "home", Now.AddSeconds(1)).Value;

        await _conversations!.AddAsync(first);

        var act = () => _conversations.AddAsync(second);
        var ex = await act.Should().ThrowAsync<MongoWriteException>();
        ex.Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
    }
}
