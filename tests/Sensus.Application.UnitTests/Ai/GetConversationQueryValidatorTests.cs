using FluentAssertions;
using Sensus.Application.Ai.Queries.GetConversation;
using Xunit;

namespace Sensus.Application.UnitTests.Ai;

public sealed class GetConversationQueryValidatorTests
{
    private readonly GetConversationQueryValidator _validator = new();

    [Theory]
    [InlineData(1, 20)]
    [InlineData(1, 1)]
    [InlineData(3, 100)]
    public void Validate_ShouldPass_WhenPageAndPageSizeAreWithinBounds(int page, int pageSize)
    {
        var result = _validator.Validate(new GetConversationQuery(Guid.NewGuid(), page, pageSize));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 101)]
    [InlineData(1, 0)]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    public void Validate_ShouldFail_WhenPageOrPageSizeAreOutOfBounds(int page, int pageSize)
    {
        var result = _validator.Validate(new GetConversationQuery(Guid.NewGuid(), page, pageSize));

        result.IsValid.Should().BeFalse();
    }
}
