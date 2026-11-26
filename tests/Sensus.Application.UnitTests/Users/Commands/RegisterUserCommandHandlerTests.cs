using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RegisterUser;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IModerationAuditRecorder _auditRecorder = Substitute.For<IModerationAuditRecorder>();
    private readonly ITextModerationService _textModerationService = Substitute.For<ITextModerationService>();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();

        _auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Sensus.Domain.Common.Result.Success());
        _textModerationService.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", null, "Approved"));

        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        _handler = new RegisterUserCommandHandler(
            _userRepository,
            _passwordHasher,
            _jwtTokenGenerator,
            _dateTimeProvider,
            _auditRecorder,
            textModerationService: _textModerationService);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenRegistrationIsValid()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "Rodrigo Perez",
            "rodrigo@sensus.com",
            "Password123!",
            true);

        _userRepository.ExistsByEmailAsync(command.Correo, Arg.Any<CancellationToken>())
            .Returns(false);

        _passwordHasher.HashPassword(command.Password)
            .Returns("$2a$11$mockedBCryptHashedPasswordString");

        _jwtTokenGenerator.GenerateToken(Arg.Any<User>())
            .Returns(("mock.jwt.token", DateTime.UtcNow.AddHours(24)));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().Be("mock.jwt.token");
        result.Value.User.Correo.Should().Be("rodrigo@sensus.com");
        result.Value.User.NombreCompleto.Should().Be("Rodrigo Perez");
        result.Value.User.Roles.Should().Contain(RoleType.Consumidor);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "Rodrigo Perez",
            "existente@sensus.com",
            "Password123!",
            true);

        _userRepository.ExistsByEmailAsync(command.Correo, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.EmailExists");

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
