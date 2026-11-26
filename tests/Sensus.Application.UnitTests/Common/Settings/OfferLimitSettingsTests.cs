using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Sensus.Application.Common.Settings;

namespace Sensus.Application.UnitTests.Common.Settings;

public sealed class OfferLimitSettingsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1001, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(1000, true)]
    public void ValidateDataAnnotations_EnforcesRange1To1000(int value, bool expectedValid)
    {
        var settings = new OfferLimitSettings { MaxActivePerMerchant = value };
        var context = new ValidationContext(settings);
        var results = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(settings, context, results, validateAllProperties: true);

        isValid.Should().Be(expectedValid);
    }
}
