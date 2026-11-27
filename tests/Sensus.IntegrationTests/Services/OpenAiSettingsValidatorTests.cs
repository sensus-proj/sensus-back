using FluentAssertions;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class OpenAiSettingsValidatorTests
{
    private readonly OpenAiSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenDefaultSettings_ReturnsSuccess()
    {
        var result = _validator.Validate(null, new OpenAiSettings());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBudgetExceeds20Seconds_ReturnsFailure()
    {
        var settings = new OpenAiSettings
        {
            TimeoutSeconds = 15,
            Moderation = new ModerationRetrySettings { MaxRetries = 1, RetryDelayMilliseconds = 500 }
        };

        var result = _validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("20 s");
    }

    [Theory]
    [InlineData(-1, 500, "MaxRetries")]
    [InlineData(3, 500, "MaxRetries")]
    [InlineData(1, -1, "RetryDelayMilliseconds")]
    [InlineData(1, 2001, "RetryDelayMilliseconds")]
    public void Validate_WhenRetrySettingsOutOfRange_ReturnsFailure(int maxRetries, int delayMs, string expectedKey)
    {
        var settings = new OpenAiSettings
        {
            TimeoutSeconds = 5,
            Moderation = new ModerationRetrySettings { MaxRetries = maxRetries, RetryDelayMilliseconds = delayMs }
        };

        var result = _validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_WhenTimeoutOutOfRange_ReturnsFailure(int timeoutSeconds)
    {
        var settings = new OpenAiSettings
        {
            TimeoutSeconds = timeoutSeconds,
            Moderation = new ModerationRetrySettings { MaxRetries = 0, RetryDelayMilliseconds = 0 }
        };

        var result = _validator.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_WhenBoundary20Seconds_ReturnsSuccess()
    {
        var settings = new OpenAiSettings
        {
            TimeoutSeconds = 10,
            Moderation = new ModerationRetrySettings { MaxRetries = 1, RetryDelayMilliseconds = 0 }
        };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeTrue();
    }
}
