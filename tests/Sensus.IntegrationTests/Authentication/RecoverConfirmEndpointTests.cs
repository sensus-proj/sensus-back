using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using Sensus.Application.Users.Commands.ResetPassword;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

/// <summary>
/// Pruebas de la ruta canónica <c>POST /api/v1/auth/recover/confirm</c> (Spec 029, FR-029-05 y FR-029-06).
/// Va en su propia clase porque la política de rate limiting de recuperación se comparte por cliente.
/// </summary>
public class RecoverConfirmEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Route = "/api/v1/auth/recover/confirm";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RecoverConfirmEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecoverConfirm_ShouldReturnNoContent_WhenTokenIsValid()
    {
        var user = User.Create(
            "Confirm Target User",
            "confirm.reset@sensus.com",
            "old-hashed-password",
            true,
            DateTime.UtcNow).Value;

        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = PasswordResetToken.ComputeHash(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, user.Correo, rawToken, _factory.FakeClock.UtcNow, 15).Value;

        _factory.PasswordResetTokenRepository.TryConsumeAsync(tokenHash, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.PasswordResetTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(resetToken);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var response = await _client.PostAsJsonAsync(Route, new ResetPasswordCommand(rawToken, "NuevaPass1234#"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.UserRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverConfirm_ShouldReturnBadRequest_WhenTokenIsExpiredOrAlreadyUsed()
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = PasswordResetToken.ComputeHash(rawToken);
        _factory.PasswordResetTokenRepository.TryConsumeAsync(tokenHash, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.PostAsJsonAsync(Route, new ResetPasswordCommand(rawToken, "NuevaPass1234#"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
