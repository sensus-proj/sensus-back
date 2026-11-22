using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Users;

public class DeleteAccountEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeleteAccountEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.UserRepository.ClearReceivedCalls();
        _factory.ComercioRepository.ClearReceivedCalls();
        _factory.PasswordResetTokenRepository.ClearReceivedCalls();
        _factory.OfferRepository.ClearReceivedCalls();
    }

    // Regresión FR-030-06 / CA-030-05: al eliminar una cuenta comercial sus ofertas no deben quedar visibles.
    [Fact]
    public async Task DeleteAccount_ShouldArchiveMerchantOffers_WhenAccountOwnsMerchant()
    {
        // Arrange
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";

        var user = User.Create(
            "Merchant to Delete",
            $"merchant.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var comercio = Comercio.Create(user.Id, "Abarrotes Prueba", "Calle 1", "42800", 20.05, -99.34).Value;
        _factory.ComercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(comercio);

        Offer published = BuildOffer(comercio.Id);
        Offer paused = BuildOffer(comercio.Id);
        paused.Pause(DateTime.UtcNow).IsSuccess.Should().BeTrue();
        _factory.OfferRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Offer> { published, paused });

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(rawPassword))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        published.Status.Should().Be(OfferStatus.Archived);
        paused.Status.Should().Be(OfferStatus.Archived);
        await _factory.OfferRepository.Received(1).UpdateAsync(published, Arg.Any<CancellationToken>());
        await _factory.OfferRepository.Received(1).UpdateAsync(paused, Arg.Any<CancellationToken>());
        await _factory.UserInterestsRepository.Received(1).DeleteByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
    }

    private static Offer BuildOffer(Guid merchantId) =>
        Offer.Create(
            merchantId,
            "Combo despensa",
            "Arroz y frijol en oferta",
            100m,
            20,
            5,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            20.05,
            -99.34,
            DateTime.UtcNow).Value;

    private string CreateTokenForUser(User user)
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturnNoContent_WhenPasswordIsCorrect()
    {
        // Arrange
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";
        var realHash = passwordHasher.HashPassword(rawPassword);

        var user = User.Create(
            "User to Delete",
            "delete.me@sensus.com",
            realHash,
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var token = CreateTokenForUser(user);

        var command = new DeleteAccountCommand(rawPassword);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.ComercioRepository.Received(1).DeleteByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.PasswordResetTokenRepository.Received(1).InvalidateExistingTokensForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.UserRepository.Received(1).DeleteAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.Services.GetRequiredService<INotificationLocationRepository>().Received(1)
            .DeleteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturnUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var realHash = passwordHasher.HashPassword("RealPassword123#");

        var user = User.Create(
            "User Wrong Pass",
            "wrong.pass@sensus.com",
            realHash,
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var token = CreateTokenForUser(user);

        var command = new DeleteAccountCommand("IncorrectPassword123#");

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 401 Unauthorized y no se borra ningún dato
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.UserRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturnBadRequest_WhenPasswordIsEmpty()
    {
        var user = User.Create(
            "User Empty Pass",
            "empty.pass@sensus.com",
            "hashed-secret",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var token = CreateTokenForUser(user);

        var command = new DeleteAccountCommand("");

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturnForbidden_WhenCallerIsGuest()
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (guestToken, _) = tokenGenerator.GenerateGuestToken();

        var command = new DeleteAccountCommand("AnyPassword123#");

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Regresión FR-046-15: al eliminar la cuenta se deben purgar todas las conversaciones, mensajes y trazas de Sensi IA
    [Fact]
    public async Task DeleteAccount_ShouldPurgeAllAiData_WhenAccountIsDeleted()
    {
        // Arrange
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";

        var user = User.Create(
            "User with Sensi Data",
            $"user.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _factory.AiConversationRepository.ClearReceivedCalls();
        _factory.AiMessageRepository.ClearReceivedCalls();
        _factory.AiTraceLogRepository.ClearReceivedCalls();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(rawPassword))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.AiConversationRepository.Received(1).PurgeAllByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.AiMessageRepository.Received(1).PurgeAllByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.AiTraceLogRepository.Received(1).PurgeAllByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
    }

    // Regresión FR-030-08 / CA-030-06: borrado inmediato de user_interests e interacciones seudonimizadas
    [Fact]
    public async Task DeleteAccount_ShouldPurgeInterestsAndInteractions_WhenAccountIsDeleted()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";

        var user = User.Create(
            "User with Feed Data",
            $"feed.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _factory.UserInterestsRepository.ClearReceivedCalls();
        _factory.OfferInteractionRecorder.ClearReceivedCalls();
        _factory.CacheService.ClearReceivedCalls();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(rawPassword))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.UserInterestsRepository.Received(1).DeleteByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.OfferInteractionRecorder.Received(1).PurgeByUserAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.CacheService.Received(1).RemoveAsync(
            Sensus.Application.Offers.FeedCacheKeys.Affinity(user.Id),
            Arg.Any<CancellationToken>());
    }
}
