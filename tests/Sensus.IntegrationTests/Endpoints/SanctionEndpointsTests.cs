using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas de integración de <c>/api/v1/moderation/sanctions</c> y del bloqueo de acceso (Spec 020):
/// éxito, entrada inválida, sin autenticación y sin permisos por cada endpoint (Principio V).
/// </summary>
public class SanctionEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Route = "/api/v1/moderation/sanctions";
    private const string Password = "Password123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SanctionEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
    }

    // ---------- Autenticación y permisos (CA-020-08) ----------

    [Theory]
    [InlineData("GET", Route)]
    [InlineData("GET", Route + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", Route)]
    [InlineData("POST", Route + "/00000000-0000-0000-0000-000000000001/revert")]
    public async Task Endpoints_WithoutToken_ReturnUnauthorized(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, token: null, body: new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", Route, RoleType.Consumidor)]
    [InlineData("GET", Route + "/00000000-0000-0000-0000-000000000001", RoleType.Comerciante)]
    [InlineData("POST", Route, RoleType.Consumidor)]
    [InlineData("POST", Route + "/00000000-0000-0000-0000-000000000001/revert", RoleType.Comerciante)]
    public async Task Endpoints_WithoutAdministratorRole_ReturnForbidden(string method, string url, string role)
    {
        var (token, _) = CreateUserToken(role);

        var response = await SendAsync(new HttpMethod(method), url, token, new { userId = Guid.NewGuid(), reason = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_WithGuestToken_ReturnsForbidden()
    {
        var (guestToken, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken();

        var response = await SendAsync(HttpMethod.Get, Route, guestToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Listado y detalle (FR-020-05) ----------

    [Fact]
    public async Task List_AsAdministrator_ReturnsPagedSanctions()
    {
        var (token, admin) = CreateUserToken(RoleType.Administrador);
        var sanction = Sanction.CreateManual(Guid.NewGuid(), admin.Id, "Acoso", _factory.FakeClock.UtcNow, 24).Value;
        _factory.SanctionRepository.ListAsync(null, null, Arg.Any<DateTime>(), 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Sanction>)[sanction], 1L));

        var response = await SendAsync(HttpMethod.Get, Route, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        json.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("items")[0].GetProperty("status").GetString().Should().Be("Active");
    }

    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=0")]
    [InlineData("?page=0")]
    [InlineData("?status=Bogus")]
    public async Task List_WithInvalidQuery_ReturnsBadRequest(string query)
    {
        var (token, _) = CreateUserToken(RoleType.Administrador);

        var response = await SendAsync(HttpMethod.Get, Route + query, token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ExistingSanction_ReturnsDetail_AndUnknownReturnsNotFound()
    {
        var (token, admin) = CreateUserToken(RoleType.Administrador);
        var sanction = Sanction.CreateManual(Guid.NewGuid(), admin.Id, "Acoso", _factory.FakeClock.UtcNow, null).Value;
        _factory.SanctionRepository.GetByIdAsync(sanction.Id, Arg.Any<CancellationToken>()).Returns(sanction);
        _factory.SanctionRepository.GetByIdAsync(Arg.Is<Guid>(id => id != sanction.Id), Arg.Any<CancellationToken>())
            .Returns((Sanction?)null);

        var ok = await SendAsync(HttpMethod.Get, $"{Route}/{sanction.Id}", token);
        var missing = await SendAsync(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", token);

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Sanción manual (FR-020-07, CA-020-10) ----------

    [Fact]
    public async Task Create_AsAdministrator_ReturnsCreatedAndBlocksTheUser()
    {
        var (token, _) = CreateUserToken(RoleType.Administrador);
        var target = RegisterUser();

        var response = await SendAsync(HttpMethod.Post, Route, token, new { userId = target.Id, reason = "Fraude", durationHours = 48 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        target.IsSuspended(_factory.FakeClock.UtcNow).Should().BeTrue();
        await _factory.SanctionRepository.Received(1).AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", 24)]
    [InlineData("x", 0)]
    [InlineData("x", 99999)]
    public async Task Create_WithInvalidBody_ReturnsBadRequest(string reason, int hours)
    {
        var (token, _) = CreateUserToken(RoleType.Administrador);

        var response = await SendAsync(HttpMethod.Post, Route, token, new { userId = Guid.NewGuid(), reason, durationHours = hours });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AgainstSelf_ReturnsForbiddenProblem()
    {
        var (token, admin) = CreateUserToken(RoleType.Administrador);

        var response = await SendAsync(HttpMethod.Post, Route, token, new { userId = admin.Id, reason = "x", durationHours = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Sanction.TargetNotAllowed");
    }

    // ---------- Reversión (FR-020-02, CA-020-02, CA-020-09) ----------

    [Fact]
    public async Task Revert_ActiveSanction_ReturnsOkAndRestoresAccess()
    {
        var (token, admin) = CreateUserToken(RoleType.Administrador);
        var target = RegisterUser();
        var sanction = Sanction.CreateAutomatic(target.Id, "x", SanctionEvidence.Manual, _factory.FakeClock.UtcNow, 24).Value;
        target.Suspend(sanction.Id, sanction.EndsAtUtc, _factory.FakeClock.UtcNow);
        _factory.SanctionRepository.GetByIdAsync(sanction.Id, Arg.Any<CancellationToken>()).Returns(sanction);

        var response = await SendAsync(HttpMethod.Post, $"{Route}/{sanction.Id}/revert", token, new { reason = "Falso positivo" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("Reverted");
        json.RootElement.GetProperty("revertedBy").GetGuid().Should().Be(admin.Id);
        target.IsSuspended(_factory.FakeClock.UtcNow).Should().BeFalse();
    }

    [Fact]
    public async Task Revert_AlreadyReverted_ReturnsConflict()
    {
        var (token, admin) = CreateUserToken(RoleType.Administrador);
        var sanction = Sanction.CreateAutomatic(Guid.NewGuid(), "x", SanctionEvidence.Manual, _factory.FakeClock.UtcNow, 24).Value;
        sanction.Revert(admin.Id, "ya", _factory.FakeClock.UtcNow);
        _factory.SanctionRepository.GetByIdAsync(sanction.Id, Arg.Any<CancellationToken>()).Returns(sanction);

        var response = await SendAsync(HttpMethod.Post, $"{Route}/{sanction.Id}/revert", token, new { reason = "otra vez" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revert_UnknownOrWithoutReason_ReturnsNotFoundOrBadRequest()
    {
        var (token, _) = CreateUserToken(RoleType.Administrador);
        var unknown = Guid.NewGuid();
        _factory.SanctionRepository.GetByIdAsync(unknown, Arg.Any<CancellationToken>()).Returns((Sanction?)null);

        var notFound = await SendAsync(HttpMethod.Post, $"{Route}/{unknown}/revert", token, new { reason = "x" });
        var badRequest = await SendAsync(HttpMethod.Post, $"{Route}/{unknown}/revert", token, new { reason = "" });

        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badRequest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Bloqueo de acceso (FR-020-04, NFR-020-01, CA-020-03, CA-020-04, CA-020-06) ----------

    [Fact]
    public async Task Login_OfSuspendedUser_ReturnsForbiddenWithEndDate_AndNoEvidence()
    {
        var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var now = _factory.FakeClock.UtcNow;
        var user = User.Create("Sancionado", $"s-{Guid.NewGuid():N}@sensus.com", hasher.HashPassword(Password), true, now).Value;
        user.Suspend(Guid.NewGuid(), now.AddHours(24), now);
        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { correo = user.Correo, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().Should().Be("Auth.UserSanctioned");
        json.RootElement.GetProperty("sanctionEndsAtUtc").GetDateTime().Should().BeCloseTo(now.AddHours(24), TimeSpan.FromSeconds(1));
        body.Should().NotContain("Evidence").And.NotContain("weapons");
    }

    [Fact]
    public async Task Login_OfSuspendedUserWithWrongPassword_DoesNotRevealTheSanction()
    {
        var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var now = _factory.FakeClock.UtcNow;
        var user = User.Create("Sancionado", $"s-{Guid.NewGuid():N}@sensus.com", hasher.HashPassword(Password), true, now).Value;
        user.Suspend(Guid.NewGuid(), null, now);
        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { correo = user.Correo, password = "Incorrecta123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Sanctioned");
    }

    [Fact]
    public async Task ExistingToken_AfterSuspension_IsRejectedImmediately_AndWorksAgainAfterReversal()
    {
        var (token, user) = CreateUserToken(RoleType.Consumidor);
        var before = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token);
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        // RestoreSuspension no rota el stamp: se aísla la comprobación de sanción de la de sesión revocada.
        var sanctionId = Guid.NewGuid();
        user.RestoreSuspension(sanctionId, _factory.FakeClock.UtcNow.AddHours(1));
        var blocked = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token);

        user.LiftSuspension(sanctionId);
        var restored = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token);

        blocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExistingToken_AfterSuspensionExpires_WorksWithoutIntervention()
    {
        var (token, user) = CreateUserToken(RoleType.Consumidor);
        user.RestoreSuspension(Guid.NewGuid(), _factory.FakeClock.UtcNow.AddMinutes(10));
        var blocked = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token);

        _factory.FakeClock.Advance(TimeSpan.FromMinutes(11));
        var after = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token);

        blocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- Auxiliares ----------

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null && method != HttpMethod.Get)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private (string Token, User User) CreateUserToken(string role)
    {
        var user = User.Create(
            "Usuario de Prueba",
            $"u-{Guid.NewGuid():N}@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;
        if (role != RoleType.Consumidor)
        {
            user.AddRole(role);
        }

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
        return (token, user);
    }

    private User RegisterUser()
    {
        var user = User.Create("Objetivo", $"t-{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }
}
