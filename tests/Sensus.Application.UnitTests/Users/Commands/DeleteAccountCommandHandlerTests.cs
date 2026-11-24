using FluentAssertions;
using MediatR;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.CloseAccountContent;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
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
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator = Substitute.For<ICdnCacheInvalidator>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly DeleteAccountCommandHandler _handler;

    public DeleteAccountCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _sender.Send(Arg.Any<CloseAccountContentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CloseAccountContentResult(0, 0, 0, 0, 0)));
        _handler = new DeleteAccountCommandHandler(
            _userRepository,
            _comercioRepository,
            _tokenRepository,
            _currentUserService,
            _passwordHasher,
            _imageStorage,
            _dateTimeProvider,
            _cdnCacheInvalidator,
            _sender);
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

    // Spec 061: el cierre de contenido se delega antes de la baja lógica de la spec 060.
    [Fact]
    public async Task Handle_ShouldInvokeContentClosure_WhenPasswordIsCorrect()
    {
        var user = ArrangeAuthenticatedUser("CorrectPassword123#");

        var result = await _handler.Handle(new DeleteAccountCommand("CorrectPassword123#"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _sender.Received(1).Send(
            Arg.Is<CloseAccountContentCommand>(c => c.UserId == user.Id),
            Arg.Any<CancellationToken>());
        await _comercioRepository.Received(1).DeleteByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotMarkAsDeleted_WhenContentClosureFails()
    {
        ArrangeAuthenticatedUser("CorrectPassword123#");
        _sender.Send(Arg.Any<CloseAccountContentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CloseAccountContentResult>(
                Error.Conflict("AccountClosure.Failed", "No se pudo cerrar el contenido.")));

        var result = await _handler.Handle(new DeleteAccountCommand("CorrectPassword123#"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AccountClosure.Failed");
        await _userRepository.DidNotReceiveWithAnyArgs().MarkAsDeletedAsync(default, default, default!, default);
        await _comercioRepository.DidNotReceiveWithAnyArgs().DeleteByUsuarioIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_ShouldMarkUserAsDeleted_WhenPasswordIsCorrect()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create(
            "Target User",
            "target@sensus.com",
            "hashed-secret",
            true,
            DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("CorrectPassword123#", "hashed-secret").Returns(true);

        var result = await _handler.Handle(
            new DeleteAccountCommand("CorrectPassword123#"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _comercioRepository.Received(1).DeleteByUsuarioIdAsync(userId, Arg.Any<CancellationToken>());
        await _tokenRepository.Received(1).InvalidateExistingTokensForUserAsync(userId, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).MarkAsDeletedAsync(
            userId,
            Arg.Is<DateTime>(d => d.Kind == DateTimeKind.Utc),
            Arg.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
            Arg.Any<CancellationToken>());
        user.IsDeleted.Should().BeTrue();
        user.Estatus.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenPasswordIsIncorrect()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create(
            "Target User",
            "target@sensus.com",
            "hashed-secret",
            true,
            DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("WrongPassword", "hashed-secret").Returns(false);

        var result = await _handler.Handle(new DeleteAccountCommand("WrongPassword"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        await _userRepository.DidNotReceiveWithAnyArgs().MarkAsDeletedAsync(default, default, default!, default);
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<CloseAccountContentCommand>(), default);
    }

    // Regresión FR-030-08 / CA-030-06: purga de intereses (054), interacciones (055) y caché de afinidad.
    [Fact]
    public async Task Handle_WhenValid_ShouldPurgeInterestsInteractionsAndAffinityCache()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create("Consumidor", "purge@sensus.com", "hashed-secret", true, DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(user, userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("ValidPass123#", "hashed-secret").Returns(true);

        var cache = Substitute.For<ICacheService>();
        var interestsRepo = Substitute.For<IUserInterestsRepository>();
        var interactionRecorder = Substitute.For<IOfferInteractionRecorder>();

        var handler = new DeleteAccountCommandHandler(
            _userRepository,
            _comercioRepository,
            _tokenRepository,
            _currentUserService,
            _passwordHasher,
            _imageStorage,
            _dateTimeProvider,
            _cdnCacheInvalidator,
            _sender,
            cacheService: cache,
            userInterestsRepository: interestsRepo,
            offerInteractionRecorder: interactionRecorder);

        var result = await handler.Handle(new DeleteAccountCommand("ValidPass123#"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await interestsRepo.Received(1).DeleteByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await interactionRecorder.Received(1).PurgeByUserAsync(userId, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(
            Sensus.Application.Offers.FeedCacheKeys.Affinity(userId),
            Arg.Any<CancellationToken>());
    }
}
