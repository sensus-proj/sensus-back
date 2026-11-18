using FluentAssertions;
using Sensus.Application.Common.Settings;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class SensiSettingsValidatorTests
{
    private readonly SensiSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenValidSettings_ReturnsSuccess()
    {
        // Arrange
        var settings = new SensiSettings
        {
            ModerationModel = "omni-moderation-latest",
            FastModel = "gpt-4o-mini",
            ReasoningModel = "gpt-4o",
            TotalBudgetCapUsd = 5.0,
            ReasoningBudgetCapUsd = 3.0,
            AlertThresholdRatio = 0.80,
            DailyMessageLimitPerUser = 20
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "gpt-4o-mini", "gpt-4o", "ModerationModel es obligatorio")]
    [InlineData("omni-moderation-latest", "", "gpt-4o", "FastModel es obligatorio")]
    [InlineData("omni-moderation-latest", "gpt-4o-mini", "", "ReasoningModel es obligatorio")]
    public void Validate_WhenMissingModels_ReturnsFailure(string modModel, string fastModel, string reasoningModel, string expectedError)
    {
        // Arrange
        var settings = new SensiSettings
        {
            ModerationModel = modModel,
            FastModel = fastModel,
            ReasoningModel = reasoningModel
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(expectedError);
    }

    [Fact]
    public void Validate_WhenReasoningCapExceedsTotalCap_ReturnsFailure()
    {
        // Arrange
        var settings = new SensiSettings
        {
            TotalBudgetCapUsd = 3.0,
            ReasoningBudgetCapUsd = 5.0
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ReasoningBudgetCapUsd debe ser mayor a 0 y no exceder TotalBudgetCapUsd");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WhenTotalBudgetCapInvalid_ReturnsFailure(double totalCap)
    {
        // Arrange
        var settings = new SensiSettings
        {
            TotalBudgetCapUsd = totalCap,
            ReasoningBudgetCapUsd = 1.0
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TotalBudgetCapUsd debe estar entre 0.1 y 100.0 USD");
    }
}
