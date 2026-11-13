using FluentAssertions;
using Sensus.Application.Offers;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Offers.DTOs;

public class OfferMappingExtensionsTests
{
    [Fact]
    public void ToDto_ShouldMapAllPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var startsAt = now.AddHours(1);
        var endsAt = now.AddDays(3);
        var modifiedAt = now.AddHours(2);

        var offer = Offer.Reconstitute(
            id,
            merchantId,
            "2x1 Tacos al Pastor",
            "Incluye salsa y limones",
            120.00m,
            25,
            90.00m,
            15,
            startsAt,
            endsAt,
            OfferStatus.Published,
            "https://cloudinary.com/tacos.jpg",
            19.4326,
            -99.1332,
            now,
            modifiedAt);

        var dto = offer.ToDto();

        dto.Id.Should().Be(id);
        dto.MerchantId.Should().Be(merchantId);
        dto.Title.Should().Be("2x1 Tacos al Pastor");
        dto.Description.Should().Be("Incluye salsa y limones");
        dto.OriginalPrice.Should().Be(120.00m);
        dto.DiscountPercentage.Should().Be(25);
        dto.DiscountedPrice.Should().Be(90.00m);
        dto.Stock.Should().Be(15);
        dto.StartsAt.Should().Be(startsAt);
        dto.EndsAt.Should().Be(endsAt);
        dto.Status.Should().Be("Published");
        dto.ImageUrl.Should().Be("https://cloudinary.com/tacos.jpg");
        dto.Latitude.Should().Be(19.4326);
        dto.Longitude.Should().Be(-99.1332);
        dto.CreatedAtUtc.Should().Be(now);
        dto.LastModifiedAtUtc.Should().Be(modifiedAt);
    }
}
