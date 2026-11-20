using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetOfferAlternatives;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Discovery;

public sealed class OfferAlternativesTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OfferAlternativesPolicy_ComparablePrice_ShouldUseFinalPriceWithin120Percent()
    {
        decimal maxFinal = OfferAlternativesPolicy.ComputeMaxComparableFinalPrice(100m, 1.20m);
        maxFinal.Should().Be(120m);

        var originalId = Guid.NewGuid();
        var keep = new OfferAlternativeCandidate(Guid.NewGuid(), "Keep", nameof(OfferCategory.Bakery), null, 200m, 120m, 3, 500, Now.AddDays(2), Guid.NewGuid(), "M1");
        var tooExpensive = new OfferAlternativeCandidate(Guid.NewGuid(), "Expensive", nameof(OfferCategory.Bakery), null, 150m, 121m, 3, 100, Now.AddDays(2), Guid.NewGuid(), "M2");
        var wrongCategory = new OfferAlternativeCandidate(Guid.NewGuid(), "Dairy", nameof(OfferCategory.Dairy), null, 80m, 60m, 3, 100, Now.AddDays(2), Guid.NewGuid(), "M3");
        var tooFar = new OfferAlternativeCandidate(Guid.NewGuid(), "Far", nameof(OfferCategory.Bakery), null, 90m, 70m, 3, 10_001, Now.AddDays(2), Guid.NewGuid(), "M4");
        var originalItself = keep with { OfferId = originalId };

        IReadOnlyList<OfferAlternativeCandidate> filtered = OfferAlternativesPolicy.FilterAndSort(
            originalId,
            OfferCategory.Bakery,
            maxFinal,
            OfferAlternativesPolicy.RegisteredMaxDistanceMeters,
            [originalItself, tooExpensive, wrongCategory, tooFar, keep],
            5);

        filtered.Should().ContainSingle().Which.OfferId.Should().Be(keep.OfferId);
    }

    [Fact]
    public void OfferAlternativesPolicy_ResolveMaxDistance_ShouldClampGuestTo2Km()
    {
        OfferAlternativesPolicy.ResolveMaxDistanceMeters(isGuest: true, 10_000).Should().Be(2_000);
        OfferAlternativesPolicy.ResolveMaxDistanceMeters(isGuest: false, 10_000).Should().Be(10_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void GetOfferAlternativesQueryValidator_ShouldRejectOutOfRangeLimit(int limit)
    {
        var validator = new GetOfferAlternativesQueryValidator();
        validator.Validate(new GetOfferAlternativesQuery(Guid.NewGuid(), limit)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferAlternativesQuery(Guid.Empty, 5)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetOfferAlternativesQueryHandler_WhenNotFoundOrNotVisible_ShouldReturnUniform404()
    {
        IOfferRepository repo = Substitute.For<IOfferRepository>();
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.IsGuest.Returns(false);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        repo.GetAlternativesAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(OfferAlternativesLookupResult.NotFoundOrUnavailable());

        var handler = new GetOfferAlternativesQueryHandler(
            repo,
            user,
            clock,
            Options.Create(new OfferAlternativesSettings()),
            NullLogger<GetOfferAlternativesQueryHandler>.Instance);

        var r1 = await handler.Handle(new GetOfferAlternativesQuery(Guid.NewGuid(), 5), CancellationToken.None);
        var r2 = await handler.Handle(new GetOfferAlternativesQuery(Guid.NewGuid(), 5), CancellationToken.None);

        r1.IsFailure.Should().BeTrue();
        r1.Error.Code.Should().Be(GetOfferAlternativesQueryHandler.UniformNotFoundCode);
        r1.Error.Message.Should().Be(r2.Error.Message);
    }

    [Fact]
    public void OfferAlternativeItemDto_Mapping_ShouldMapExplicitly()
    {
        var candidate = new OfferAlternativeCandidate(
            Guid.NewGuid(),
            "Pan dulce",
            "Panadería",
            null,
            80m,
            50m,
            2,
            456.4,
            Now.AddHours(12),
            Guid.NewGuid(),
            "Panadería Centro");

        OfferAlternativeItemDto dto = candidate.ToDto();

        dto.OfferId.Should().Be(candidate.OfferId);
        dto.Category.Should().Be(nameof(OfferCategory.Bakery));
        dto.DistanceMeters.Should().Be(456);
        dto.FinalPrice.Should().Be(50m);
        dto.MerchantName.Should().Be("Panadería Centro");
    }
}
