using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Offers.Entities;

public class OfferViewCountTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegisterView_ShouldIncrementCounter()
    {
        Offer offer = BuildOffer();

        offer.RegisterView().IsSuccess.Should().BeTrue();
        offer.RegisterView().IsSuccess.Should().BeTrue();

        offer.ViewCount.Should().Be(2);
    }

    [Fact]
    public void RegisterView_ShouldFail_WhenOfferIsArchived()
    {
        Offer offer = BuildOffer();
        offer.Archive(Now);

        var result = offer.RegisterView();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ArchivedNotViewable");
        offer.ViewCount.Should().Be(0);
    }

    [Fact]
    public void Reconstitute_ShouldRestoreViewCount()
    {
        Offer offer = BuildOffer();

        Offer restored = Offer.Reconstitute(
            offer.Id,
            offer.MerchantId,
            offer.Title,
            offer.Description,
            offer.OriginalPrice,
            offer.DiscountPercentage,
            offer.DiscountedPrice,
            offer.Stock,
            offer.StartsAt,
            offer.EndsAt,
            offer.Status,
            offer.ImageUrl,
            offer.Latitude,
            offer.Longitude,
            offer.CreatedAtUtc,
            offer.LastModifiedAtUtc,
            42);

        restored.ViewCount.Should().Be(42);
    }

    private static Offer BuildOffer() =>
        Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            Now.AddHours(-1),
            Now.AddDays(1),
            null,
            -12.05,
            -77.04,
            Now).Value;
}
