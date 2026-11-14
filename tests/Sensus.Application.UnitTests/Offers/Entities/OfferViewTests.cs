using FluentAssertions;
using Sensus.Domain.Entities;

namespace Sensus.Application.UnitTests.Offers.Entities;

public class OfferViewTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 18, 45, 30, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldNormalizeDayToUtcMidnight()
    {
        var result = OfferView.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ViewedOn.Should().Be(new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc));
        result.Value.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Create_ShouldFail_WhenUserIsEmpty()
    {
        var result = OfferView.Create(Guid.Empty, Guid.NewGuid(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferView.UserRequired");
    }

    [Fact]
    public void Create_ShouldFail_WhenOfferIsEmpty()
    {
        var result = OfferView.Create(Guid.NewGuid(), Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferView.OfferRequired");
    }

    [Fact]
    public void Create_ShouldProduceSameDeduplicationKey_ForTwoViewsOfTheSameDay()
    {
        Guid userId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();

        OfferView morning = OfferView.Create(userId, offerId, Now.Date.AddHours(8)).Value;
        OfferView evening = OfferView.Create(userId, offerId, Now.Date.AddHours(22)).Value;

        morning.UserId.Should().Be(evening.UserId);
        morning.OfferId.Should().Be(evening.OfferId);
        morning.ViewedOn.Should().Be(evening.ViewedOn);
        morning.Id.Should().NotBe(evening.Id);
    }

    [Fact]
    public void Create_ShouldProduceDifferentKey_ForDifferentDays()
    {
        Guid userId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();

        OfferView today = OfferView.Create(userId, offerId, Now).Value;
        OfferView tomorrow = OfferView.Create(userId, offerId, Now.AddDays(1)).Value;

        tomorrow.ViewedOn.Should().BeAfter(today.ViewedOn);
    }
}
