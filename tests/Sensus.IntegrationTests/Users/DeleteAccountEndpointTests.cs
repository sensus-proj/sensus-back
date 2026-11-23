using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Application.Users.Commands.Login;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;
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
        await _factory.UserRepository.Received(1).MarkAsDeletedAsync(
            user.Id,
            Arg.Is<DateTime>(d => d.Kind == DateTimeKind.Utc),
            Arg.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
            Arg.Any<CancellationToken>());
        user.IsDeleted.Should().BeTrue();
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
        await _factory.UserRepository.DidNotReceiveWithAnyArgs().MarkAsDeletedAsync(default, default, default!, default);
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

    // Regresión FR-060-01 / CA-060-01 y CA-060-02: la baja conserva el documento y revoca el token al instante.
    [Fact]
    public async Task DeleteAccount_ShouldKeepDocumentAndRejectPreviousToken_Immediately()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";
        var user = User.Create(
            "Soft Deleted User",
            $"soft.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        string token = CreateTokenForUser(user);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(rawPassword))
        };
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage deleteResponse = await _client.SendAsync(deleteRequest);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        user.IsDeleted.Should().BeTrue();
        user.Correo.Should().NotBeEmpty();

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage profileResponse = await _client.SendAsync(profileRequest);

        profileResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // CA-060-03: el login de una cuenta dada de baja devuelve 401 genérico aunque la contraseña sea correcta.
    [Fact]
    public async Task Login_ShouldReturnGenericUnauthorized_WhenAccountWasSoftDeleted()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";
        var user = User.Create(
            "Deleted Login User",
            $"deleted.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            DateTime.UtcNow).Value;
        user.MarkAsDeleted(_factory.FakeClock.UtcNow);

        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(user.Correo, rawPassword));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.UserRepository.Received(1).GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>());
    }

    // Spec 061 (CA-061-01 a CA-061-06): anonimiza contenido, expira apartados antes de archivar y
    // copia la evidencia de moderación a una auditoría que no contiene PII.
    [Fact]
    public async Task DeleteAccount_ShouldAnonymizeContentExpireClaimsBeforeArchivingAndAuditWithoutPii()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var rawPassword = $"Sensus-{Guid.NewGuid():N}-Aa1#";
        DateTime now = _factory.FakeClock.UtcNow;

        _factory.FeedPostRepository.ClearReceivedCalls();
        _factory.FeedCommentRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();
        _factory.SanctionRepository.ClearReceivedCalls();
        _factory.ModerationAuditRepository.ClearReceivedCalls();
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.ComercioRepository.ClearReceivedCalls();

        var user = User.Create(
            "Ana Perez",
            $"ana.{Guid.NewGuid():N}@sensus.com",
            passwordHasher.HashPassword(rawPassword),
            true,
            now.AddDays(-30)).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        FeedPost post = FeedPost.Create(user.Id, "Ana Perez", "Contenido ya moderado", null, now.AddDays(-2)).Value;
        FeedComment.Create(post.Id, user.Id, "Ana Perez", "Comentario ya moderado", now.AddDays(-1));
        _factory.FeedPostRepository
            .AnonymizeByAuthorAsync(user.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(1);
        _factory.FeedCommentRepository
            .AnonymizeByAuthorAsync(user.Id, Arg.Any<CancellationToken>()).Returns(1);

        var comercio = Comercio.Create(user.Id, "Tienda Ana", "Calle 2", "42800", 20.05, -99.34).Value;
        _factory.ComercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        Offer offer = BuildOffer(comercio.Id);
        _factory.OfferRepository.GetByMerchantIdAsync(comercio.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { offer });
        _factory.OfferClaimRepository
            .ExpireActiveByMerchantAsync(comercio.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(2);

        var evidence = new SanctionEvidence(
            Guid.NewGuid(),
            "Rejected",
            ["violence"],
            new Dictionary<string, double> { ["violence"] = 0.93 },
            "0.85",
            "Quarantine");
        Sanction sanction = Sanction.CreateAutomatic(
            user.Id, "Contenido rechazado", evidence, now.AddDays(-5), null).Value;
        _factory.SanctionRepository.GetAllByUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { sanction });

        ModerationAudit? captured = null;
        _factory.ModerationAuditRepository.AddIfNotExistsAsync(
            Arg.Do<ModerationAudit>(entry => captured = entry), Arg.Any<CancellationToken>()).Returns(true);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(rawPassword))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenForUser(user));

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.FeedPostRepository.Received(1).AnonymizeByAuthorAsync(
            user.Id,
            Arg.Is<DateTime>(d => d.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
        await _factory.FeedCommentRepository.Received(1).AnonymizeByAuthorAsync(user.Id, Arg.Any<CancellationToken>());
        await _factory.OfferClaimRepository.Received(1).ExpireActiveByMerchantAsync(
            comercio.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        offer.Status.Should().Be(OfferStatus.Archived);

        Received.InOrder(() =>
        {
            _factory.OfferClaimRepository.ExpireActiveByMerchantAsync(
                comercio.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
            _factory.OfferRepository.UpdateAsync(offer, Arg.Any<CancellationToken>());
        });

        captured.Should().NotBeNull();
        captured!.SubjectKey.Should().Be(ModerationAudit.ComputeSubjectKey(user.Id));
        captured.SubjectKey.Should().NotContain(user.Id.ToString());
        string serialized = System.Text.Json.JsonSerializer.Serialize(captured);
        serialized.Should().NotContain(user.Correo);
        serialized.Should().NotContain(user.NombreCompleto);
        captured.Decision.Should().Be("Rejected");
        captured.Categories.Should().ContainSingle().Which.Should().Be("violence");
        captured.Scores["violence"].Should().Be(0.93);
        captured.ExpiresAtUtc.Should().BeAfter(now);
    }
}
