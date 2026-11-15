using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

public class DeleteAccountCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IProfileImageStorage _imageStorage = Substitute.For<IProfileImageStorage>();
    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly DeleteAccountCommandHandler _handler;

    public DeleteAccountCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _handler = new DeleteAccountCommandHandler(
            _userRepository,
            _comercioRepository,
            _tokenRepository,
            _currentUserService,
            _passwordHasher,
            _imageStorage,
            _offerRepository,
            _dateTimeProvider);
    }

    private User ArrangeAuthenticatedUser(string password)
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        var user = User.Create("Target User", "target@sensus.com", "hashed-secret", true, DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(password, "hashed-secret").Returns(true);
        return user;
    }

    private static Offer BuildOffer(Guid merchantId) =>
        Offer.Create(merchantId, "Combo despensa", "Arroz y frijol en oferta", 100m, 20, 5,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1), null, 20.05, -99.34, DateTime.UtcNow).Value;

    [Fact]
    public async Task Handle_ShouldArchiveNonArchivedOffers_WhenUserOwnsMerchant()
    {
        // Arrange
        var user = ArrangeAuthenticatedUser("CorrectPassword123#");
        var comercio = Comercio.Create(user.Id, "Abarrotes Prueba", "Calle 1", "42800", 20.05, -99.34).Value;
        _comercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        Offer published = BuildOffer(comercio.Id);
        Offer archived = BuildOffer(comercio.Id);
        archived.Archive(DateTime.UtcNow);
        _offerRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Offer> { published, archived });

        // Act
        var result = await _handler.Handle(new DeleteAccountCommand("CorrectPassword123#"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        published.Status.Should().Be(OfferStatus.Archived);
        await _offerRepository.Received(1).UpdateAsync(published, Arg.Any<CancellationToken>());
        await _offerRepository.DidNotReceive().UpdateAsync(archived, Arg.Any<CancellationToken>());
        await _comercioRepository.Received(1).DeleteByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotQueryOffers_WhenUserHasNoMerchant()
    {
        // Arrange
        ArrangeAuthenticatedUser("CorrectPassword123#");

        // Act
        var result = await _handler.Handle(new DeleteAccountCommand("CorrectPassword123#"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _offerRepository.DidNotReceiveWithAnyArgs().GetByMerchantIdAsync(default, default);
        await _offerRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldDeleteUserComercioAndTokens_WhenPasswordIsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create(
            "Target User",
            "target@sensus.com",
            "hashed-secret",
            true,
            DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("CorrectPassword123#", "hashed-secret")
            .Returns(true);

        var command = new DeleteAccountCommand("CorrectPassword123#");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _comercioRepository.Received(1).DeleteByUsuarioIdAsync(userId, Arg.Any<CancellationToken>());
        await _tokenRepository.Received(1).InvalidateExistingTokensForUserAsync(userId, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).DeleteAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create(
            "Target User",
            "target@sensus.com",
            "hashed-secret",
            true,
            DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("WrongPassword", "hashed-secret")
            .Returns(false);

        var command = new DeleteAccountCommand("WrongPassword");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        await _userRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
