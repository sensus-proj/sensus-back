using FluentAssertions;
using Sensus.Domain.Policies;

namespace Sensus.Application.UnitTests.Offers.Policies;

public class NearbyOffersRadiusPolicyTests
{
    [Theory]
    [InlineData(1, 1000)]
    [InlineData(3, 3000)]
    [InlineData(5, 5000)]
    public void ResolveRadiusMeters_ShouldReturnRequestedRadius_ForRegisteredUserInRange(double requestedKm, int expectedMeters)
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: false, requestedKm);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedMeters);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(6)]
    public void ResolveRadiusMeters_ShouldRejectOutOfRangeRadius_ForRegisteredUser(double requestedKm)
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: false, requestedKm);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.RadiusOutOfRange");
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(2, 2000)]
    [InlineData(5, 2000)]
    [InlineData(0.5, 500)]
    public void ResolveRadiusMeters_ShouldClampGuestRadiusToTwoKilometers(double requestedKm, int expectedMeters)
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: true, requestedKm);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedMeters);
    }

    [Fact]
    public void ResolveRadiusMeters_ShouldRejectRadiusAboveTechnicalLimit_EvenForGuest()
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: true, 80);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.RadiusTooLarge");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ResolveRadiusMeters_ShouldRejectInvalidValues(double requestedKm)
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: false, requestedKm);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.RadiusInvalid");
    }

    [Fact]
    public void ResolveRadiusMeters_ShouldUseDefault_WhenRadiusIsOmitted()
    {
        var result = NearbyOffersRadiusPolicy.ResolveRadiusMeters(isGuest: false, requestedRadiusKm: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(NearbyOffersRadiusPolicy.DefaultRadiusKm * 1000);
    }
}
