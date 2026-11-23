using FluentAssertions;
using FluentValidation.TestHelper;
using Sensus.Application.Moderation.Audit;
using Xunit;

namespace Sensus.Application.UnitTests.Moderation.Audit;

/// <summary>
/// Pruebas del validador de consulta de auditoría de moderación (Spec 021).
/// Ref: FR-021-03 - validación de filtros y límites de paginación (20 por defecto, 100 máximo).
/// </summary>
public sealed class GetModerationAuditQueryValidatorTests
{
    private readonly GetModerationAuditQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultValues_ShouldPassValidation()
    {
        // Ref: FR-021-03 - valores válidos por defecto
        var query = new GetModerationAuditQuery();
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithExplicitValidValues_ShouldPassValidation()
    {
        // Ref: FR-021-03 - consulta con filtros válidos
        DateTime now = DateTime.UtcNow;
        var query = new GetModerationAuditQuery(
            TargetId: Guid.NewGuid(),
            TargetType: "MediaAsset",
            Verdict: "Approved",
            FromUtc: now.AddDays(-1),
            ToUtc: now,
            Page: 5,
            PageSize: 100);

        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WhenPageIsLessThanOne_ShouldHaveValidationError(int page)
    {
        // Ref: FR-021-03 - página base 1
        var query = new GetModerationAuditQuery(Page: page);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page)
            .WithErrorMessage("El número de página debe ser mayor o igual a 1.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    [InlineData(500)]
    public void Validate_WhenPageSizeIsOutOfRange_ShouldHaveValidationError(int pageSize)
    {
        // Ref: FR-021-03 - tamaño de página entre 1 y 100
        var query = new GetModerationAuditQuery(PageSize: pageSize);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData("InvalidTarget")]
    [InlineData("Post")]
    [InlineData("UnknownType")]
    public void Validate_WhenTargetTypeIsInvalid_ShouldHaveValidationError(string targetType)
    {
        // Ref: FR-021-03 - tipo de objetivo permitido
        var query = new GetModerationAuditQuery(TargetType: targetType);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.TargetType)
            .WithErrorMessage("El tipo de objetivo debe ser MediaAsset, FeedComment, Offer, StoreReview, Comercio o User.");
    }

    [Theory]
    [InlineData("MediaAsset")]
    [InlineData("feedcomment")]
    [InlineData("OFFER")]
    [InlineData("StoreReview")]
    [InlineData("Comercio")]
    [InlineData("user")]
    public void Validate_WhenTargetTypeIsValidCaseInsensitive_ShouldPassValidation(string targetType)
    {
        var query = new GetModerationAuditQuery(TargetType: targetType);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.TargetType);
    }

    [Theory]
    [InlineData("InvalidVerdict")]
    [InlineData("Pending")]
    [InlineData("Unknown")]
    public void Validate_WhenVerdictIsInvalid_ShouldHaveValidationError(string verdict)
    {
        // Ref: FR-021-03 - veredicto permitido
        var query = new GetModerationAuditQuery(Verdict: verdict);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Verdict)
            .WithErrorMessage("El veredicto debe ser Approved, Quarantined o Rejected.");
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("quarantined")]
    [InlineData("REJECTED")]
    public void Validate_WhenVerdictIsValidCaseInsensitive_ShouldPassValidation(string verdict)
    {
        var query = new GetModerationAuditQuery(Verdict: verdict);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Verdict);
    }

    [Fact]
    public void Validate_WhenToUtcIsBeforeFromUtc_ShouldHaveValidationError()
    {
        // Ref: FR-021-03 - consistencia temporal
        DateTime now = DateTime.UtcNow;
        var query = new GetModerationAuditQuery(
            FromUtc: now,
            ToUtc: now.AddDays(-1));

        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.ToUtc)
            .WithErrorMessage("La fecha final no puede ser anterior a la fecha inicial.");
    }
}
