using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Queries.GetCurrentUser;
using Sensus.Domain.Common;

namespace Sensus.Application.UnitTests.Users.Queries;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task Handle_MapsAllFieldsAndDeduplicatesRoles()
    {
        var userId = Guid.NewGuid().ToString();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _currentUser.FullName.Returns("Rodrigo Sánchez");
        _currentUser.Email.Returns("rodrigo@sensus.app");
        _currentUser.Roles.Returns(["Consumidor", "Comerciante", "consumidor"]);

        var handler = new GetCurrentUserQueryHandler(_currentUser);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.NombreCompleto.Should().Be("Rodrigo Sánchez");
        result.Value.Correo.Should().Be("rodrigo@sensus.app");
        result.Value.Roles.Should().Equal("Consumidor", "Comerciante");
    }

    [Fact]
    public async Task Handle_MapsAbsentClaimsToNull()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("guest-123");
        _currentUser.FullName.Returns((string?)null);
        _currentUser.Email.Returns((string?)null);
        _currentUser.Roles.Returns(["Invitado"]);

        var handler = new GetCurrentUserQueryHandler(_currentUser);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("guest-123");
        result.Value.NombreCompleto.Should().BeNull();
        result.Value.Correo.Should().BeNull();
        result.Value.Roles.Should().Equal("Invitado");
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var handler = new GetCurrentUserQueryHandler(_currentUser);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }
}
