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

    [Fact]
    public void Create_ShouldFail_WhenMerchantIdIsEmpty()
    {
        var result = Offer.Create(
            Guid.Empty,
            "Título válido",
            "Descripción válida",
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.MerchantIdRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]
    public void Create_ShouldFail_WhenTitleIsTooShortOrEmpty(string title)
    {
        var result = Offer.Create(
            _merchantId,
            title,
            "Descripción válida",
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidTitle");
    }

    [Fact]
    public void Create_ShouldFail_WhenTitleIsTooLong()
    {
        var longTitle = new string('A', 101);
        var result = Offer.Create(
            _merchantId,
            longTitle,
            "Descripción válida",
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidTitle");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public void Create_ShouldFail_WhenDescriptionIsTooShortOrEmpty(string desc)
    {
        var result = Offer.Create(
            _merchantId,
            "Título válido",
            desc,
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidDescription");
    }

    [Fact]
    public void Create_ShouldFail_WhenDescriptionIsTooLong()
    {
        var longDesc = new string('D', 501);
        var result = Offer.Create(
            _merchantId,
            "Título válido",
            longDesc,
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidDescription");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_ShouldFail_WhenOriginalPriceIsZeroOrNegative(decimal price)
    {
        var result = Offer.Create(
            _merchantId,
            "Título válido",
            "Descripción válida",
            price,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidOriginalPrice");
    }

    [Fact]
    public void Create_ShouldFail_WhenEndsAtIsInThePast()
    {
        var result = Offer.Create(
            _merchantId,
            "Título válido",
            "Descripción válida",
            100m,
            10,
            5,
            _now.AddDays(-2),
            _now.AddHours(-1),
            nowUtc: _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ExpiredEndDate");
    }

    [Fact]
    public void Expire_ShouldReturnSuccess_WhenOfferIsAlreadyExpired()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Ya expirada",
            "Descripción",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        var result = offer.Expire(_now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Expired);
    }

    [Fact]
    public void Republish_ShouldFail_WhenNewEndsAtIsBeforeNewStartsAt()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Expirada",
            "Descripción",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        var result = offer.Republish(_now.AddDays(2), _now.AddDays(1), 10, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidValidityDates");
    }

    [Fact]
    public void Republish_ShouldFail_WhenNewEndsAtIsInThePast()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Expirada",
            "Descripción",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        var result = offer.Republish(_now.AddDays(-2), _now.AddHours(-1), 10, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ExpiredEndDate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Republish_ShouldFail_WhenNewStockIsLessThanOne(int stock)
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Expirada",
            "Descripción",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Expired,
            null,
            null,
            null,
            _now.AddDays(-5));

        var result = offer.Republish(_now.AddHours(1), _now.AddDays(2), stock, _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.InvalidStock");
    }

    [Fact]
    public void Republish_ShouldSucceed_WhenOfferIsArchived()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Archivada",
            "Descripción",
            100m,
            10,
            90m,
            0,
            _now.AddDays(-5),
            _now.AddDays(-1),
            OfferStatus.Archived,
            null,
            null,
            null,
            _now.AddDays(-5));

        var newStartsAt = _now.AddHours(1);
        var newEndsAt = _now.AddDays(3);
        var result = offer.Republish(newStartsAt, newEndsAt, 20, _now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Published);
        offer.Stock.Should().Be(20);
        offer.StartsAt.Should().Be(newStartsAt);
        offer.EndsAt.Should().Be(newEndsAt);
    }

    [Fact]
    public void Pause_ShouldSucceed_WhenOfferIsPublished()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Publicada",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        var result = offer.Pause(_now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Paused);
    }

    [Fact]
    public void Pause_ShouldFail_WhenOfferIsNotPublished()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Pausada",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            OfferStatus.Paused,
            null,
            null,
            null,
            _now);

        var result = offer.Pause(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CannotPause");
    }

    [Fact]
    public void Resume_ShouldSucceed_WhenOfferIsPausedAndNotExpired()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Pausada",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(-1),
            _now.AddDays(2),
            OfferStatus.Paused,
            null,
            null,
            null,
            _now.AddHours(-1));

        var result = offer.Resume(_now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Published);
    }

    [Fact]
    public void Resume_ShouldFail_WhenOfferIsNotPaused()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Publicada",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        var result = offer.Resume(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.CannotResume");
    }

    [Fact]
    public void Resume_ShouldTransitionToExpiredAndFail_WhenOfferVigencyPassed()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Pausada que ya venció",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddDays(-3),
            _now.AddHours(-1), // vencida
            OfferStatus.Paused,
            null,
            null,
            null,
            _now.AddDays(-3));

        var result = offer.Resume(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.OfferExpired");
        offer.Status.Should().Be(OfferStatus.Expired);
    }

    [Fact]
    public void Archive_ShouldSetStatusToArchived()
    {
        var offer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Por archivar",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(1),
            _now.AddDays(2),
            OfferStatus.Published,
            null,
            null,
            null,
            _now);

        var result = offer.Archive(_now);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Archived);
    }

    [Fact]
    public void IsActive_ShouldReturnExpectedBooleans()
    {
        var validOffer = Offer.Reconstitute(
            Guid.NewGuid(),
            _merchantId,
            "Activa",
            "Descripción",
            100m,
            10,
            90m,
            5,
            _now.AddHours(-1),
            _now.AddHours(5),
            OfferStatus.Published,
            null,
            null,
            null,
            _now.AddHours(-1));

        validOffer.IsActive(_now).Should().BeTrue();

        // Sin stock
        var zeroStockOffer = Offer.Reconstitute(
            Guid.NewGuid(), _merchantId, "Sin stock", "Desc", 100m, 10, 90m, 0,
            _now.AddHours(-1), _now.AddHours(5), OfferStatus.Published, null, null, null, _now.AddHours(-1));
        zeroStockOffer.IsActive(_now).Should().BeFalse();

        // Aún no inicia
        var futureOffer = Offer.Reconstitute(
            Guid.NewGuid(), _merchantId, "Futura", "Desc", 100m, 10, 90m, 5,
            _now.AddHours(2), _now.AddHours(5), OfferStatus.Published, null, null, null, _now);
        futureOffer.IsActive(_now).Should().BeFalse();

        // Ya expiró
        var expiredOffer = Offer.Reconstitute(
            Guid.NewGuid(), _merchantId, "Expirada", "Desc", 100m, 10, 90m, 5,
            _now.AddDays(-2), _now.AddHours(-1), OfferStatus.Published, null, null, null, _now.AddDays(-2));
        expiredOffer.IsActive(_now).Should().BeFalse();

        // Estado Paused
        var pausedOffer = Offer.Reconstitute(
            Guid.NewGuid(), _merchantId, "Pausada", "Desc", 100m, 10, 90m, 5,
            _now.AddHours(-1), _now.AddHours(5), OfferStatus.Paused, null, null, null, _now.AddHours(-1));
        pausedOffer.IsActive(_now).Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldIncrementVersionAndReturnContentChanged_WhenDataIsValid()
    {
        var offer = Offer.Create(
            _merchantId,
            "Título original",
            "Descripción original",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            null,
            null,
            _now).Value;

        offer.Version.Should().Be(1);

        var result = offer.Update(
            "Título modificado",
            "Descripción original",
            120m,
            20,
            15,
            _now.AddHours(2),
            _now.AddDays(3),
            null,
            activeClaimCount: 0,
            expectedVersion: 1,
            _now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue("porque el título cambió");
        offer.Version.Should().Be(2);
        offer.Title.Should().Be("Título modificado");
        offer.OriginalPrice.Should().Be(120m);
        offer.DiscountPercentage.Should().Be(20);
        offer.DiscountedPrice.Should().Be(96m);
        offer.Stock.Should().Be(15);
    }

    [Fact]
    public void Update_ShouldReturnFalseForContentChanged_WhenOnlyPricingOrDatesChange()
    {
        var offer = Offer.Create(
            _merchantId,
            "Título original",
            "Descripción original",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            null,
            null,
            _now).Value;

        var result = offer.Update(
            "Título original",
            "Descripción original",
            150m,
            30,
            20,
            _now.AddHours(2),
            _now.AddDays(4),
            null,
            activeClaimCount: 0,
            expectedVersion: 1,
            _now.AddMinutes(10));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse("porque ni el título, ni la descripción ni la imagen cambiaron");
        offer.Version.Should().Be(2);
    }

    [Fact]
    public void Update_ShouldFail_WhenVersionMismatch()
    {
        var offer = Offer.Create(
            _merchantId,
            "Título original",
            "Descripción original",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            null,
            null,
            _now).Value;

        var result = offer.Update(
            "Título modificado",
            "Descripción original",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            activeClaimCount: 0,
            expectedVersion: 5,
            _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.VersionConflict");
    }

    [Fact]
    public void Update_ShouldFail_WhenStockIsLessThanActiveClaims()
    {
        var offer = Offer.Create(
            _merchantId,
            "Título original",
            "Descripción original",
            100m,
            10,
            10,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            null,
            null,
            _now).Value;

        var result = offer.Update(
            "Título original",
            "Descripción original",
            100m,
            10,
            stock: 2,
            _now.AddHours(1),
            _now.AddDays(2),
            null,
            activeClaimCount: 5,
            expectedVersion: 1,
            _now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.StockBelowActiveClaims");
    }

    [Fact]
    public void Create_AndUpdate_ShouldPersistOptionalProductCategory()
    {
        var offer = Offer.Create(
            _merchantId,
            "Bolillo recién horneado",
            "Pan artesanal del día",
            20m,
            25,
            12,
            _now.AddHours(1),
            _now.AddDays(1),
            null,
            20.0536,
            -99.3436,
            _now,
            "Panadería").Value;

        offer.Category.Should().Be("Panadería");

        var updated = offer.Update(
            "Bolillo recién horneado",
            "Pan artesanal del día",
            20m,
            25,
            10,
            _now.AddHours(1),
            _now.AddDays(1),
            null,
            activeClaimCount: 0,
            expectedVersion: 1,
            _now,
            "Abarrotes");

        updated.IsSuccess.Should().BeTrue();
        offer.Category.Should().Be("Abarrotes");
    }
}
