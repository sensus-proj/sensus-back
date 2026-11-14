using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.RedeemClaim;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class RedeemClaimCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private const string ValidCode = "v1.abc.def.123.signature";
    private const string CodeHash = "hash";

    private readonly IOfferClaimRepository _offerClaimRepository = Substitute.For<IOfferClaimRepository>();
    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IClaimCodeService _claimCodeService = Substitute.For<IClaimCodeService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly RedeemClaimCommandHandler _handler;

    public RedeemClaimCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(UserId.ToString());
        _dateTimeProvider.UtcNow.Returns(Now);
        _claimCodeService.ComputeHash(ValidCode).Returns(CodeHash);

        _handler = new RedeemClaimCommandHandler(
            _offerClaimRepository,
            _offerRepository,
            _comercioRepository,
            _currentUserService,
            _claimCodeService,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldRedeemClaim_WhenCodeAndOwnerAreValid()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        OfferClaim claim = BuildClaim(comercio.Id, OfferClaimStatus.Active, Now.AddHours(1));
        SetupValidCode(claim);
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);
        _offerClaimRepository.TryRedeemAsync(claim.Id, comercio.Id, Now, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand(ValidCode), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ClaimId.Should().Be(claim.Id);
        result.Value.MerchantId.Should().Be(comercio.Id);
        await _offerClaimRepository.Received(1)
            .TryRedeemAsync(claim.Id, comercio.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenCodeIsInvalid()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand("bad-code"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.CodeInvalid");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenMerchantIsNotOwner()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        OfferClaim claim = BuildClaim(Guid.NewGuid(), OfferClaimStatus.Active, Now.AddHours(1));
        SetupValidCode(claim);
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand(ValidCode), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.NotOwner");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenClaimWasAlreadyRedeemed()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        OfferClaim claim = BuildClaim(comercio.Id, OfferClaimStatus.Redeemed, Now.AddHours(1));
        SetupValidCode(claim);
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand(ValidCode), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.AlreadyRedeemed");
    }

    [Fact]
    public async Task Handle_ShouldReleaseStockAndExpire_WhenClaimElapsed()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        OfferClaim claim = BuildClaim(comercio.Id, OfferClaimStatus.Active, Now.AddMinutes(-1));
        SetupValidCode(claim);
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand(ValidCode), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.Expired");
        claim.Status.Should().Be(OfferClaimStatus.Expired);
        await _offerRepository.Received(1).ReturnStockAsync(claim.OfferId, 1, Arg.Any<CancellationToken>());
        await _offerClaimRepository.Received(1).UpdateAsync(claim, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenAtomicRedeemLosesTheRace()
    {
        // Arrange
        Comercio comercio = BuildComercio();
        OfferClaim claim = BuildClaim(comercio.Id, OfferClaimStatus.Active, Now.AddHours(1));
        SetupValidCode(claim);
        _comercioRepository.GetByUsuarioIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(comercio);
        _offerClaimRepository.TryRedeemAsync(claim.Id, comercio.Id, Now, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(new RedeemClaimCommand(ValidCode), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OfferClaim.AlreadyRedeemed");
    }

    private void SetupValidCode(OfferClaim claim)
    {
        _claimCodeService.TryValidate(ValidCode, Now, out Arg.Any<ClaimCodePayload>())
            .Returns(call =>
            {
                call[2] = new ClaimCodePayload(claim.Id, claim.OfferId, claim.ExpiresAtUtc);
                return true;
            });

        _offerClaimRepository.GetByCodeHashAsync(CodeHash, Arg.Any<CancellationToken>()).Returns(claim);
    }

    private static Comercio BuildComercio()
    {
        return Comercio.Create(
            UserId,
            "Tienda",
            "Calle 1",
            "06000",
            19.4,
            -99.1,
            Now,
            MerchantCategory.Otros).Value;
    }

    private static OfferClaim BuildClaim(Guid merchantId, OfferClaimStatus status, DateTime expiresAtUtc)
    {
        OfferClaim claim = OfferClaim.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            merchantId,
            CodeHash,
            status,
            expiresAtUtc,
            null,
            Now.AddHours(-1));

        return claim;
    }
}
