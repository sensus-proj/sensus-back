using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Xunit;

namespace Sensus.Application.UnitTests.Common.Moderation;

/// <summary>
/// Pruebas unitarias para ContentModerationHelper y ModerationEvaluation (Spec 021).
/// Ref: FR-021-01 - veredicto, puntajes y umbral de cada decisión.
/// </summary>
public sealed class ContentModerationHelperTests
{
    private readonly ITextModerationService _moderationService = Substitute.For<ITextModerationService>();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EvaluateAsync_WhenTextIsNullOrEmpty_ReturnsApproved(string? text)
    {
        // Ref: FR-021-01 - textos vacíos aprobados sin llamar al motor externo
        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            text,
            "test_context",
            "Error.Code",
            "Default error message",
            CancellationToken.None);

        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be(ContentModerationHelper.ApprovedDecision);
        result.Verdict.Should().Be(ModerationVerdict.Approved);
        result.RejectionError.Should().BeNull();
        await _moderationService.DidNotReceive().ModerateContentTextAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Esto es una mierda total")]
    [InlineData("Eres un pendejo")]
    [InlineData("Vete a la verga")]
    public async Task EvaluateAsync_WhenTextContainsProfanity_ReturnsRejectedWithoutCallingExternalService(string text)
    {
        // Ref: FR-021-01 - filtro de palabras altisonantes local
        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            text,
            "test_context",
            "Error.Profanity",
            "Mensaje por defecto",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be(ContentModerationHelper.RejectedDecision);
        result.Verdict.Should().Be(ModerationVerdict.Rejected);
        result.RejectionError.Should().NotBeNull();
        result.RejectionError!.Code.Should().Be("Error.Profanity");
        await _moderationService.DidNotReceive().ModerateContentTextAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_WhenModerationServiceIsNullAndTextIsNotEmpty_ReturnsQuarantineFailClosed()
    {
        // Ref: FR-017-09 - moderación fail-closed cuando no hay servicio configurado
        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            null,
            "Texto limpio y normal",
            "test_context",
            "Error.Code",
            "Mensaje por defecto",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be(ContentModerationHelper.QuarantineDecision);
        result.Verdict.Should().Be(ModerationVerdict.Quarantined);
        result.Message.Should().Be("La moderación no está disponible; el contenido no fue publicado.");
        result.RejectionError.Should().NotBeNull();
        result.RejectionError!.Code.Should().Be("Error.Code.ModerationUnavailable");
        result.RejectionError.Type.Should().Be(ErrorType.Failure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EvaluateAsync_WhenModerationServiceIsNullAndTextIsEmpty_ReturnsApproved(string? text)
    {
        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            null,
            text,
            "test_context",
            "Error.Code",
            "Mensaje por defecto",
            CancellationToken.None);

        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be(ContentModerationHelper.ApprovedDecision);
        result.Verdict.Should().Be(ModerationVerdict.Approved);
        result.RejectionError.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WhenModerationServiceThrows_ReturnsQuarantineFailClosed()
    {
        _moderationService.ModerateContentTextAsync("Texto válido", "test_context", Arg.Any<CancellationToken>())
            .Returns<TextModerationResult>(_ => throw new HttpRequestException("OpenAI timeout"));

        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            "Texto válido",
            "test_context",
            "Error.Code",
            "Mensaje por defecto",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be(ContentModerationHelper.QuarantineDecision);
        result.Verdict.Should().Be(ModerationVerdict.Quarantined);
        result.RejectionError.Should().NotBeNull();
        result.RejectionError!.Code.Should().Be("Error.Code.ModerationUnavailable");
    }

    [Fact]
    public async Task EvaluateAsync_WhenModerationServiceReturnsNull_ReturnsQuarantineFailClosed()
    {
        _moderationService.ModerateContentTextAsync("Texto válido", "test_context", Arg.Any<CancellationToken>())
            .Returns((TextModerationResult)null!);

        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            "Texto válido",
            "test_context",
            "Error.Code",
            "Mensaje por defecto",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be(ContentModerationHelper.QuarantineDecision);
        result.Verdict.Should().Be(ModerationVerdict.Quarantined);
        result.RejectionError.Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureApprovedAsync_WhenModerationServiceIsNull_ReturnsFailure()
    {
        Result result = await ContentModerationHelper.EnsureApprovedAsync(
            null,
            "Texto limpio y normal",
            "context",
            "Error.Code",
            "Error message",
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Code.ModerationUnavailable");
        result.Error.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public async Task EvaluateAsync_WhenServiceReturnsApproved_ReturnsApprovedEvaluation()
    {
        var serviceResult = new TextModerationResult(
            IsApproved: true,
            Decision: "Approved",
            Message: "Aprobado",
            FlaggedCategories: ["harmless"],
            CategoryScores: new Dictionary<string, double> { ["harmless"] = 0.01 });

        _moderationService.ModerateContentTextAsync("Buena oferta", "offer_context", Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            "Buena oferta",
            "offer_context",
            "Offer.Rejected",
            "Error",
            CancellationToken.None);

        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
        result.Verdict.Should().Be(ModerationVerdict.Approved);
        result.FlaggedCategories.Should().Equal("harmless");
        result.CategoryScores.Should().ContainKey("harmless");
    }

    [Fact]
    public async Task EvaluateAsync_WhenServiceReturnsQuarantine_ReturnsQuarantineEvaluation()
    {
        var serviceResult = new TextModerationResult(
            IsApproved: false,
            Decision: "Quarantine",
            Message: "Requiere revisión humana",
            FlaggedCategories: ["suspicious"],
            CategoryScores: new Dictionary<string, double> { ["suspicious"] = 0.65 });

        _moderationService.ModerateContentTextAsync("Texto dudoso", "offer_context", Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            "Texto dudoso",
            "offer_context",
            "Offer.Quarantine",
            "Error",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Verdict.Should().Be(ModerationVerdict.Quarantined);
        result.Message.Should().Be("Requiere revisión humana");
        result.RejectionError!.Code.Should().Be("Offer.Quarantine");
    }

    [Fact]
    public async Task EvaluateAsync_WhenServiceReturnsRejected_ReturnsRejectedEvaluation()
    {
        var serviceResult = new TextModerationResult(
            IsApproved: false,
            Decision: "Rejected",
            Message: "Contenido de odio",
            FlaggedCategories: ["hate"],
            CategoryScores: new Dictionary<string, double> { ["hate"] = 0.99 });

        _moderationService.ModerateContentTextAsync("Texto de odio", "offer_context", Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        ModerationEvaluation result = await ContentModerationHelper.EvaluateAsync(
            _moderationService,
            "Texto de odio",
            "offer_context",
            "Offer.Rejected",
            "Error",
            CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Verdict.Should().Be(ModerationVerdict.Rejected);
        result.Message.Should().Be("Contenido de odio");
        result.RejectionError!.Code.Should().Be("Offer.Rejected");
    }

    [Fact]
    public async Task EnsureApprovedAsync_WhenApproved_ReturnsSuccess()
    {
        _moderationService.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(IsApproved: true, Decision: "Approved"));

        Result result = await ContentModerationHelper.EnsureApprovedAsync(
            _moderationService,
            "Texto válido",
            "context",
            "Error.Code",
            "Error message",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureApprovedAsync_WhenRejected_ReturnsFailureWithError()
    {
        _moderationService.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(IsApproved: false, Decision: "Rejected", Message: "Infracción", FlaggedCategories: ["hate"]));

        Result result = await ContentModerationHelper.EnsureApprovedAsync(
            _moderationService,
            "Texto con infracción",
            "context",
            "Error.Infraction",
            "Infracción por defecto",
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Infraction");
        result.Error.Message.Should().Be("Infracción");
    }

    [Theory]
    [InlineData("approved", ModerationVerdict.Approved)]
    [InlineData("Approved", ModerationVerdict.Approved)]
    [InlineData("APPROVED", ModerationVerdict.Approved)]
    [InlineData("quarantine", ModerationVerdict.Quarantined)]
    [InlineData("Quarantine", ModerationVerdict.Quarantined)]
    [InlineData("rejected", ModerationVerdict.Rejected)]
    [InlineData("unknown", ModerationVerdict.Rejected)]
    [InlineData("", ModerationVerdict.Rejected)]
    [InlineData(null, ModerationVerdict.Rejected)]
    public void ToVerdict_MapsStringsToExpectedEnum(string? decision, ModerationVerdict expected)
    {
        ModerationVerdict verdict = ContentModerationHelper.ToVerdict(decision);
        verdict.Should().Be(expected);
    }
}
