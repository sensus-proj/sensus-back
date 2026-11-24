using FluentValidation.TestHelper;
using Sensus.Application.Reports.Commands.CreateContentReport;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Reports.Commands;

public sealed class CreateContentReportCommandValidatorTests
{
    private readonly CreateContentReportCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        var command = new CreateContentReportCommand(
            "MediaAsset",
            Guid.NewGuid(),
            "InappropriateContent",
            "Detalles válidos");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenTargetTypeIsBlank_ShouldHaveValidationError(string? targetType)
    {
        var command = new CreateContentReportCommand(
            targetType!,
            Guid.NewGuid(),
            "InappropriateContent",
            null);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TargetType);
    }

    [Fact]
    public void Validate_WhenTargetIdIsEmpty_ShouldHaveValidationError()
    {
        var command = new CreateContentReportCommand(
            "MediaAsset",
            Guid.Empty,
            "InappropriateContent",
            null);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TargetId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenReasonIsBlank_ShouldHaveValidationError(string? reason)
    {
        var command = new CreateContentReportCommand(
            "MediaAsset",
            Guid.NewGuid(),
            reason!,
            null);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Validate_WhenDetailsExceedMaxLength_ShouldHaveValidationError()
    {
        string longDetails = new('a', ContentReport.MaxDetailsLength + 1);
        var command = new CreateContentReportCommand(
            "MediaAsset",
            Guid.NewGuid(),
            "Reason",
            longDetails);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Details);
    }
}
