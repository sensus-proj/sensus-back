using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Offers.Entities;

public class OfferTests
{
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldSucceed_WhenDataIsValid()
    {
        var startsAt = _now.AddHours(1);
        var endsAt = _now.AddDays(3);

        var result = Offer.Create(
            _merchantId,
            "2x1 en Hamburguesas",
            "Válido en sucursal centro",
            100.00m,
            20,
            10,
            startsAt,
            endsAt,
            "https://res.cloudinary.com/demo/image.png",
            19.4326,
            -99.1332,
            _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be(_merchantId);
        result.Value.Title.Should().Be("2x1 en Hamburguesas");
        result.Value.OriginalPrice.Should().Be(100.00m);
        result.Value.DiscountPercentage.Should().Be(20);
        result.Value.DiscountedPrice.Should().Be(80.00m);
        result.Value.Stock.Should().Be(10);
        result.Value.Status.Should().Be(OfferStatus.Published);
    }

    [Fact]
    public void Create_ShouldCalculateDiscountedPriceCorrectly()
    {
        var startsAt = _now.AddHours(1);
        var endsAt = _now.AddDays(2);

        var result = Offer.Create(
            _merchantId,
            "Descuento especial",
            "Descripción de prueba",
            150.00m,
            33,
            5,
            startsAt,
            endsAt,
            nowUtc: _now);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountedPrice.Should().Be(100.50m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    [InlineData(-5)]
    public void Create_ShouldFail_WhenDiscountIsOutOfRange(int discount)
    {
        var startsAt = _now.AddHours(1);
        var endsAt = _now.AddDays(2);

        var result = Offer.Create(
            _merchantId,
            "Oferta inválida",
            "Descripción de prueba",
            100.00m,
            discount,
            5,
            startsAt,
            endsAt,
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidDiscount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldFail_WhenStockIsLessThanOne(int stock)
    {
        var startsAt = _now.AddHours(1);
        var endsAt = _now.AddDays(2);

        var result = Offer.Create(
            _merchantId,
            "Oferta sin stock",
            "Descripción de prueba",
            100.00m,
            10,
            stock,
            startsAt,
            endsAt,
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidStock");
    }

    [Fact]
    public void Create_ShouldFail_WhenEndsAtIsBeforeStartsAt()
    {
        var startsAt = _now.AddDays(2);
        var endsAt = _now.AddDays(1);

        var result = Offer.Create(
            _merchantId,
            "Oferta fecha inconsistente",
            "Descripción de prueba",
            100.00m,
            10,
            5,
            startsAt,
            endsAt,
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidValidityDates");
    }

    [Fact]
    public void Expire_ShouldTransitionToExpired_WhenNowIsAfterEndsAt()
    {
        var startsAt = _now.AddDays(-2);
        var endsAt = _now.AddHours(-1); // Ya finalizó

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Oferta por expirar",
            "Descripción",
            100.00m,
            10,
            90.00m,
            5,
            startsAt,
            endsAt,
            OfferStatus.Published,
            null,
            null,
            null,
            startsAt);

        var expireResult = offer.Expire(_now);

        expireResult.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Expired);
    }

    [Fact]
    public void Expire_ShouldFail_WhenNowIsBeforeEndsAt()
    {
        var startsAt = _now.AddHours(-1);
        var endsAt = _now.AddHours(5); // Aún vigente

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Oferta activa",
            "Descripción",
            100.00m,
            10,
            90.00m,
            5,
            startsAt,
            endsAt,
            OfferStatus.Published,
            null,
            null,
            null,
            startsAt);

        var expireResult = offer.Expire(_now);

        expireResult.IsFailure.Should().BeTrue();
        expireResult.Error.Code.Should().Be("Offer.NotYetExpired");
        offer.Status.Should().Be(OfferStatus.Published);
    }

    [Fact]
    public void Republish_ShouldSucceed_WhenOfferIsExpiredAndNewDatesAreValid()
    {
        var oldStartsAt = _now.AddDays(-5);
        var oldEndsAt = _now.AddDays(-1);

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Oferta pasada",
            "Descripción",
            100.00m,
            20,
            80.00m,
            0,
            oldStartsAt,
            oldEndsAt,
            OfferStatus.Expired,
            null,
            null,
            null,
            oldStartsAt);

        var newStartsAt = _now.AddHours(1);
        var newEndsAt = _now.AddDays(4);

        var result = offer.Republish(newStartsAt, newEndsAt, 15, _now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Published);
        offer.StartsAt.Should().Be(newStartsAt);
        offer.EndsAt.Should().Be(newEndsAt);
        offer.Stock.Should().Be(15);
    }

    [Fact]
    public void Republish_ShouldFail_WhenOfferIsCurrentlyPublished()
    {
        var startsAt = _now.AddHours(-1);
        var endsAt = _now.AddDays(2);

        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Oferta activa",
            "Descripción",
            100.00m,
            20,
            80.00m,
            5,
            startsAt,
            endsAt,
            OfferStatus.Published,
            null,
            null,
            null,
            startsAt);

        var result = offer.Republish(_now.AddHours(1), _now.AddDays(3), 10, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CannotRepublishActive");
    }
}
