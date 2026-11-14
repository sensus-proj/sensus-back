using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Offers.Entities;

public class OfferClaimTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldCreateActiveClaimWith24HourLifetime()
    {
        var result = OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OfferClaimStatus.Active);
        result.Value.ExpiresAtUtc.Should().Be(Now.AddHours(24));
        result.Value.CodeHash.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldFail_WhenIdsAreEmpty()
    {
        OfferClaim.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now).IsFailure.Should().BeTrue();
        OfferClaim.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now).IsFailure.Should().BeTrue();
        OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldFail_WhenLifetimeIsNotPositive()
    {
        var result = OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, TimeSpan.Zero);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.InvalidLifetime");
    }

    [Fact]
    public void AssignCodeHash_ShouldFail_WhenCodeHashIsEmpty()
    {
        OfferClaim claim = CreateClaim();

        claim.AssignCodeHash(" ").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AssignCodeHash_ShouldFail_WhenAlreadyAssigned()
    {
        OfferClaim claim = OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now).Value;
        claim.AssignCodeHash("hash-1").IsSuccess.Should().BeTrue();

        var result = claim.AssignCodeHash("hash-2");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.CodeAlreadyAssigned");
    }

    [Fact]
    public void Redeem_ShouldMarkClaimAsRedeemed_WhenOwnerAndNotExpired()
    {
        OfferClaim claim = CreateClaim();

        var result = claim.Redeem(claim.MerchantId, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OfferClaimStatus.Redeemed);
        claim.RedeemedAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Redeem_ShouldFail_WhenAlreadyRedeemed()
    {
        OfferClaim claim = CreateClaim();
        claim.Redeem(claim.MerchantId, Now.AddHours(1));

        var result = claim.Redeem(claim.MerchantId, Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.AlreadyRedeemed");
    }

    [Fact]
    public void Redeem_ShouldFail_WhenMerchantIsNotOwner()
    {
        OfferClaim claim = CreateClaim();

        var result = claim.Redeem(Guid.NewGuid(), Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.NotOwner");
        claim.Status.Should().Be(OfferClaimStatus.Active);
    }

    [Fact]
    public void Redeem_ShouldExpireAndFail_WhenLifetimeElapsed()
    {
        OfferClaim claim = CreateClaim();

        var result = claim.Redeem(claim.MerchantId, Now.AddHours(25));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.Expired");
        claim.Status.Should().Be(OfferClaimStatus.Expired);
    }

    [Fact]
    public void Expire_ShouldFail_BeforeExpiration()
    {
        OfferClaim claim = CreateClaim();

        var result = claim.Expire(Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.NotYetExpired");
    }

    [Fact]
    public void Expire_ShouldMarkClaimAsExpired()
    {
        OfferClaim claim = CreateClaim();

        var result = claim.Expire(Now.AddHours(25));

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OfferClaimStatus.Expired);
    }

    [Fact]
    public void IsActive_ShouldConsiderStatusAndExpiration()
    {
        OfferClaim claim = CreateClaim();

        claim.IsActive(Now).Should().BeTrue();
        claim.IsActive(Now.AddHours(25)).Should().BeFalse();
    }

    private static OfferClaim CreateClaim()
    {
        OfferClaim claim = OfferClaim.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now).Value;
        claim.AssignCodeHash("hash");
        return claim;
    }
}
