using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using Sensus.Application.Users.Commands.RequestPasswordReset;
using Sensus.Application.Users.Commands.ResetPassword;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

public class PasswordResetEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Recover_ShouldReturnAcceptedAndSendEmail_WhenUserExists()
    {
        var email = "recover.test@sensus.com";
        var user = User.Create("User Recover Test", email, "hashed-password", true, DateTime.UtcNow).Value;

        _factory.PasswordResetTokenRepository.ClearReceivedCalls();
        _factory.UserRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/recover", new RequestPasswordResetCommand(email));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await _factory.PasswordResetTokenRepository.Received(1)
            .AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
        _factory.FakeEmailSender.SentEmails.Should().Contain(e => e.To == email);
    }

    [Fact]
    public async Task Recover_ShouldReturnAcceptedAndNotSendEmail_WhenUserDoesNotExist()
    {
        var email = "nonexistent@sensus.com";
        _factory.UserRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var initialCount = _factory.FakeEmailSender.SentEmails.Count;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/recover", new RequestPasswordResetCommand(email));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _factory.FakeEmailSender.SentEmails.Count.Should().Be(initialCount);
    }

    [Fact]
    public async Task Recover_ShouldReturnIdenticalBody_ForExistingAndNonExistingEmail()
    {
        var existingEmail = "reveal.existing@sensus.com";
        var missingEmail = "reveal.missing@sensus.com";
        _factory.UserRepository.GetByEmailAsync(existingEmail, Arg.Any<CancellationToken>())
            .Returns(User.Create("Existing", existingEmail, "hash", true, DateTime.UtcNow).Value);
        _factory.UserRepository.GetByEmailAsync(missingEmail, Arg.Any<CancellationToken>()).Returns((User?)null);

        var existingResponse = await _client.PostAsJsonAsync("/api/v1/auth/recover", new RequestPasswordResetCommand(existingEmail));
        var missingResponse = await _client.PostAsJsonAsync("/api/v1/auth/recover", new RequestPasswordResetCommand(missingEmail));

        existingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await existingResponse.Content.ReadAsStringAsync()).Should()
            .Be(await missingResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Recover_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recover",
            new RequestPasswordResetCommand("invalid-email-format"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecoverConfirm_ShouldReturnNoContent_WhenTokenIsValid()
    {
        var email = "target.reset@sensus.com";
        var user = User.Create("Reset Target User", email, "old-hashed-password", true, DateTime.UtcNow).Value;
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = PasswordResetToken.ComputeHash(rawToken);

        var resetToken = PasswordResetToken.Create(user.Id, user.Correo, rawToken, _factory.FakeClock.UtcNow).Value;

        _factory.PasswordResetTokenRepository.TryConsumeAsync(tokenHash, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.PasswordResetTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(resetToken);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recover/confirm",
            new ResetPasswordCommand(rawToken, "NuevaPass1234#"));

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

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/recover/confirm",
            new ResetPasswordCommand(rawToken, "NuevaPass1234#"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_Alias_ShouldReturnNotFound_AfterRemoval()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordCommand(Guid.NewGuid().ToString("N"), "NuevaPass1234#"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recover_LatencyShouldMeetNfr()
    {
        var elapsed = new List<long>();

        for (var i = 0; i < 20; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await _client.PostAsJsonAsync(
                "/api/v1/auth/recover",
                new RequestPasswordResetCommand($"latency.{i}@example.com"));
            stopwatch.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            elapsed.Add(stopwatch.ElapsedMilliseconds);
        }

        var ordered = elapsed.OrderBy(value => value).ToArray();
        var p95Index = Math.Ceiling(ordered.Length * 0.95) - 1;
        ordered[(int)p95Index].Should().BeLessThan(700);
    }
}
