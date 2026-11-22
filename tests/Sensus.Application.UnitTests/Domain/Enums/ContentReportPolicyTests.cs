// Ref: FR-040-08 / Spec 040 - Políticas de reporte de contenido y filtros legales
using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Domain.Enums;

public sealed class ContentReportPolicyTests
{
    [Theory]
    [InlineData("csam")]
    [InlineData("Contenido con abuso infantil")]
    [InlineData("Sospecha de abuso sexual infantil")]
    [InlineData("Posible explotacion infantil")]
    [InlineData("explotacion sexual infantil en publicacion")]
    [InlineData("pornografia infantil")]
    [InlineData("child abuse material")]
    [InlineData("child sexual content")]
    [InlineData("child exploitation evidence")]
    [InlineData("child pornography detected")]
    [InlineData("underage sexual behavior")]
    public void IsProhibitedLegalContent_WithProhibitedTerms_ShouldReturnTrue(string text)
    {
        ContentReportPolicy.IsProhibitedLegalContent("spam", text).Should().BeTrue();
    }

    [Fact]
    public void IsProhibitedLegalContent_WithBenignContent_ShouldReturnFalse()
    {
        ContentReportPolicy.IsProhibitedLegalContent("spam", "Este producto no llego y es una estafa").Should().BeFalse();
        ContentReportPolicy.IsProhibitedLegalContent(null, null).Should().BeFalse();
    }

    [Theory]
    [InlineData("OFFER", ReportTargetType.Offer)]
    [InlineData("oferta", ReportTargetType.Offer)]
    [InlineData("POST", ReportTargetType.Post)]
    [InlineData("publicacion", ReportTargetType.Post)]
    [InlineData("COMMENT", ReportTargetType.Comment)]
    [InlineData("comentario", ReportTargetType.Comment)]
    public void TryParseTargetType_WithValidInputs_ShouldReturnTrue(string input, ReportTargetType expected)
    {
        bool success = ContentReportPolicy.TryParseTargetType(input, out ReportTargetType actual);
        success.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown_type")]
    public void TryParseTargetType_WithInvalidInputs_ShouldReturnFalse(string? input)
    {
        bool success = ContentReportPolicy.TryParseTargetType(input, out _);
        success.Should().BeFalse();
    }

    [Theory]
    [InlineData("SPAM", ReportReason.Spam)]
    [InlineData("OFENSIVO", ReportReason.Offensive)]
    [InlineData("offensive", ReportReason.Offensive)]
    [InlineData("FRAUDE", ReportReason.Fraud)]
    [InlineData("fraud", ReportReason.Fraud)]
    [InlineData("FALSE_OFFER", ReportReason.FalseOffer)]
    [InlineData("oferta_falsa", ReportReason.FalseOffer)]
    [InlineData("PRICE_MISMATCH", ReportReason.PriceMismatch)]
    [InlineData("precio_incorrecto", ReportReason.PriceMismatch)]
    [InlineData("OTRO", ReportReason.Other)]
    [InlineData("other", ReportReason.Other)]
    public void TryParseReason_WithValidInputs_ShouldReturnTrue(string input, ReportReason expected)
    {
        bool success = ContentReportPolicy.TryParseReason(input, out ReportReason actual);
        success.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid_reason")]
    public void TryParseReason_WithInvalidInputs_ShouldReturnFalse(string? input)
    {
        bool success = ContentReportPolicy.TryParseReason(input, out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void ToApiCode_ShouldReturnExpectedStrings()
    {
        ReportTargetType.Offer.ToApiCode().Should().Be("offer");
        ReportTargetType.Post.ToApiCode().Should().Be("post");
        ReportTargetType.Comment.ToApiCode().Should().Be("comment");
        ((ReportTargetType)999).ToApiCode().Should().Be("offer");

        ReportReason.Spam.ToApiCode().Should().Be("spam");
        ReportReason.Offensive.ToApiCode().Should().Be("ofensivo");
        ReportReason.Fraud.ToApiCode().Should().Be("fraude");
        ReportReason.FalseOffer.ToApiCode().Should().Be("false_offer");
        ReportReason.PriceMismatch.ToApiCode().Should().Be("price_mismatch");
        ReportReason.Other.ToApiCode().Should().Be("otro");
        ((ReportReason)999).ToApiCode().Should().Be("otro");
    }

    [Fact]
    public void ContentReport_Create_ValidationRules()
    {
        var now = DateTime.UtcNow;

        // Reporter empty
        var r1 = ContentReport.Create(Guid.Empty, ReportTargetType.Offer, Guid.NewGuid(), ReportReason.Spam, "detalle", now);
        r1.IsFailure.Should().BeTrue();
        r1.Error.Code.Should().Be("ContentReport.InvalidReporter");

        // Target empty
        var r2 = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Offer, Guid.Empty, ReportReason.Spam, "detalle", now);
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be("ContentReport.InvalidTarget");

        // Details too long
        var longDetails = new string('x', ContentReport.MaxDetailsLength + 1);
        var r3 = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Offer, Guid.NewGuid(), ReportReason.Spam, longDetails, now);
        r3.IsFailure.Should().BeTrue();
        r3.Error.Code.Should().Be("ContentReport.DetailsTooLong");

        // Prohibited legal content
        var r4 = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Offer, Guid.NewGuid(), ReportReason.Spam, "Reporte con child sexual content", now);
        r4.IsFailure.Should().BeTrue();
        r4.Error.Code.Should().Be("ContentReport.ProhibitedLegalContent");

        // Valid report
        var r5 = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Offer, Guid.NewGuid(), ReportReason.Spam, "  Spam publicitario  ", now);
        r5.IsSuccess.Should().BeTrue();
        r5.Value.Details.Should().Be("Spam publicitario");
        r5.Value.WindowBucket.Should().NotBeEmpty();
    }
}
