using FluentAssertions;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Xunit;

namespace Sensus.Application.UnitTests.Ai;

/// <summary>
/// Invariantes de <see cref="AiMessage"/> y <see cref="AiTraceLog"/> (FR-046-01, FR-046-15: auditoría redactada
/// con TTL).
/// </summary>
public sealed class AiMessageAndTraceLogTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AiMessage_Create_ShouldTrimContent_AndCopyCollections()
    {
        var result = AiMessage.Create(Guid.NewGuid(), Guid.NewGuid(), AiMessageRole.Assistant, "  Hola  ", true, "fallback", null, ["o1"], ["search_offers"], Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.RedactedContent.Should().Be("Hola");
        result.Value.Degraded.Should().BeTrue();
        result.Value.DegradationReason.Should().Be("fallback");
        result.Value.OffersCitedIds.Should().Equal("o1");
        result.Value.ToolsInvoked.Should().Equal("search_offers");
        result.Value.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void AiMessage_Create_ShouldDefaultToEmptyCollections_WhenNull()
    {
        var message = AiMessage.Create(Guid.NewGuid(), Guid.NewGuid(), AiMessageRole.User, "Hola", false, null, null, null, null, Now).Value;

        message.OffersCitedIds.Should().BeEmpty();
        message.ToolsInvoked.Should().BeEmpty();
        message.RecommendedPackage.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false, "Hola", "Ai.InvalidConversationId")]
    [InlineData(false, true, "Hola", "Ai.InvalidOwnerUserId")]
    [InlineData(false, false, "   ", "Ai.EmptyMessageContent")]
    public void AiMessage_Create_ShouldRejectInvalidInput(bool emptyConversation, bool emptyOwner, string content, string expectedCode)
    {
        var result = AiMessage.Create(
            emptyConversation ? Guid.Empty : Guid.NewGuid(),
            emptyOwner ? Guid.Empty : Guid.NewGuid(),
            AiMessageRole.User,
            content,
            false,
            null,
            null,
            null,
            null,
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void AiMessage_Reconstitute_ShouldPreserveAllFields()
    {
        var id = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var owner = Guid.NewGuid();

        var message = AiMessage.Reconstitute(id, conversationId, owner, AiMessageRole.SystemNotice, "aviso", false, null, null, ["o2"], [], Now);

        message.Id.Should().Be(id);
        message.ConversationId.Should().Be(conversationId);
        message.OwnerUserId.Should().Be(owner);
        message.Role.Should().Be(AiMessageRole.SystemNotice);
        message.OffersCitedIds.Should().Equal("o2");
    }

    [Fact]
    public void AiTraceLog_Create_ShouldTrimFields_ClampNegatives_AndApplyRetention()
    {
        var result = AiTraceLog.Create(
            Guid.NewGuid(), Guid.NewGuid(), " corr ", " 1.0.0 ", " hash ", AiRoutingTier.Reasoning, " gpt-4o ",
            " prompt ", " respuesta ", ["search_offers"], -5, -1, -2, -10, -0.5m, Now, TimeSpan.FromDays(7));

        result.IsSuccess.Should().BeTrue();
        var log = result.Value;
        log.CorrelationId.Should().Be("corr");
        log.MarkdownVersion.Should().Be("1.0.0");
        log.MarkdownHashSha256.Should().Be("hash");
        log.ModelAliasInternal.Should().Be("gpt-4o");
        log.RedactedPrompt.Should().Be("prompt");
        log.RedactedResponse.Should().Be("respuesta");
        log.RoutingTier.Should().Be(AiRoutingTier.Reasoning);
        log.ToolsInvokedSummary.Should().Equal("search_offers");
        log.PromptTokens.Should().Be(0);
        log.CachedTokens.Should().Be(0);
        log.CompletionTokens.Should().Be(0);
        log.TotalLatencyMs.Should().Be(0);
        log.EstimatedCostUsd.Should().Be(0m);
        log.CreatedAtUtc.Should().Be(Now);
        log.ExpiresAtUtc.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public void AiTraceLog_Create_ShouldDefaultTo30Days_WhenRetentionIsNotPositive()
    {
        var log = AiTraceLog.Create(
            Guid.NewGuid(), Guid.NewGuid(), "c", "1.0.0", "h", AiRoutingTier.Fast, "m", "p", "r", null, 10, 5, 3, 100, 0.001m, Now, TimeSpan.Zero).Value;

        log.ExpiresAtUtc.Should().Be(Now.AddDays(30));
        log.ToolsInvokedSummary.Should().BeEmpty();
        log.PromptTokens.Should().Be(10);
        log.EstimatedCostUsd.Should().Be(0.001m);
    }

    [Theory]
    [InlineData(true, false, "Ai.InvalidTraceConversation")]
    [InlineData(false, true, "Ai.InvalidTraceOwner")]
    public void AiTraceLog_Create_ShouldRejectMissingIdentifiers(bool emptyConversation, bool emptyOwner, string expectedCode)
    {
        var result = AiTraceLog.Create(
            emptyConversation ? Guid.Empty : Guid.NewGuid(),
            emptyOwner ? Guid.Empty : Guid.NewGuid(),
            "c", "1.0.0", "h", AiRoutingTier.Fast, "m", "p", "r", null, 0, 0, 0, 0, 0m, Now, TimeSpan.FromDays(30));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void AiTraceLog_Reconstitute_ShouldPreserveAllFields()
    {
        var id = Guid.NewGuid();
        var expires = Now.AddDays(30);

        var log = AiTraceLog.Reconstitute(
            id, Guid.NewGuid(), Guid.NewGuid(), "c", "1.0.0", "h", AiRoutingTier.Fast, "m", "p", "r",
            ["get_my_claims"], 1, 2, 3, 4, 0.5m, Now, expires);

        log.Id.Should().Be(id);
        log.ToolsInvokedSummary.Should().Equal("get_my_claims");
        log.CompletionTokens.Should().Be(3);
        log.TotalLatencyMs.Should().Be(4);
        log.EstimatedCostUsd.Should().Be(0.5m);
        log.ExpiresAtUtc.Should().Be(expires);
    }
}
