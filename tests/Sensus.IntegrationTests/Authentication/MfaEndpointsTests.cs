using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.EnrollMfa;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

public sealed class MfaEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MfaEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminFullMfaLifecycle_EnrollsLoginChallengeVerifiesAndAllowsStepUpRoutes()
    {
        var now = DateTime.UtcNow.AddSeconds(-60);
        _factory.FakeClock.Set(now);
        try
        {
            var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
            var totp = _factory.Services.GetRequiredService<ITotpService>();
            var jwtGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();

            var admin = User.Create("Admin MFA", $"admin.{Guid.NewGuid():N}@sensus.app", hasher.HashPassword("ClaveAdmin123!"), true, now).Value;
            admin.AddRole(RoleType.Administrador);

            _factory.UserRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
            _factory.UserRepository.GetByEmailAsync(admin.Correo, Arg.Any<CancellationToken>()).Returns(admin);

            // 1. Token inicial sin amr=mfa NO puede entrar a AdminOnlyPolicy
            var (baseToken, _) = jwtGen.GenerateToken(admin);
            using (var blockedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit"))
            {
                blockedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
                var blockedResp = await _client.SendAsync(blockedReq);
                blockedResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            }

            // 2. Enroll TOTP
            using var enrollReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/enroll");
            enrollReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
            var enrollResp = await _client.SendAsync(enrollReq);
            enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var enrollDto = await enrollResp.Content.ReadFromJsonAsync<EnrollMfaResponseDto>();
            enrollDto.Should().NotBeNull();
            enrollDto!.RecoveryCodes.Should().HaveCount(8);

            // 3. Activar con código TOTP válido
            var activationCode = totp.ComputeTotp(enrollDto.Secret, now);
            using var activateReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
            {
                Content = JsonContent.Create(new { code = activationCode })
            };
            activateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
            var activateResp = await _client.SendAsync(activateReq);
            activateResp.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Siguiente Login devuelve desafío intermedio sin sesión operativa
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { correo = admin.Correo, password = "ClaveAdmin123!" });
            loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var loginDto = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginDto!.RequiresMfa.Should().BeTrue();
            loginDto.MfaChallengeToken.Should().NotBeNullOrWhiteSpace();
            loginDto.Token.Should().BeNullOrEmpty();

            // 5. El challenge token NO sirve en /auth/me ni en AdminOnly
            using (var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginDto.MfaChallengeToken);
                var meResp = await _client.SendAsync(meReq);
                meResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            // 6. Verificar desafío en ventana +1 paso -> JWT con amr=mfa y acceso AdminOnly 200 OK
            var stepPlusOne = now.AddSeconds(30);
            _factory.FakeClock.Set(stepPlusOne);
            var stepUpCode = totp.ComputeTotp(enrollDto.Secret, stepPlusOne);
            using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
            {
                Content = JsonContent.Create(new { code = stepUpCode })
            };
            verifyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginDto.MfaChallengeToken);
            var verifyResp = await _client.SendAsync(verifyReq);
            verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var stepUpAuth = await verifyResp.Content.ReadFromJsonAsync<AuthResponseDto>();
            stepUpAuth!.Token.Should().NotBeNullOrWhiteSpace();

            var parsedJwt = new JwtSecurityTokenHandler().ReadJwtToken(stepUpAuth.Token);
            parsedJwt.Claims.Should().Contain(c => c.Type == "amr" && c.Value == "mfa");
            parsedJwt.Claims.Should().Contain(c => c.Type == "mfa_at");

            using var adminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit");
            adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stepUpAuth.Token);
            var adminResp = await _client.SendAsync(adminReq);
            adminResp.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Token con mfa_at vencido (> 15 min) es rechazado en AdminOnly
            var expiredStepUpTime = stepPlusOne.AddMinutes(-20);
            var (expiredStepUpToken, _) = jwtGen.GenerateAdminStepUpToken(admin, expiredStepUpTime);
            using var staleAdminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit");
            staleAdminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredStepUpToken);
            var staleResp = await _client.SendAsync(staleAdminReq);
            staleResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _factory.FakeClock.Set(DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task EnrollMfa_ForNonAdminUser_ReturnsForbidden()
    {
        var now = DateTime.UtcNow;
        _factory.FakeClock.Set(now);
        var consumer = User.Create("Consumidor", $"c.{Guid.NewGuid():N}@sensus.app", "hash", true, now).Value;
        _factory.UserRepository.GetByIdAsync(consumer.Id, Arg.Any<CancellationToken>()).Returns(consumer);
        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(consumer);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/enroll");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyMfa_ConsumesSingleUseRecoveryCode_AndFiveInvalidAttemptsLockOut()
    {
        var now = DateTime.UtcNow.AddSeconds(-30);
        _factory.FakeClock.Set(now);
        try
        {
            var jwtGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
            var admin = User.Create("Admin Lockout", $"lock.{Guid.NewGuid():N}@sensus.app", "hash", true, now).Value;
            admin.AddRole(RoleType.Administrador);
            _factory.UserRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

            var (baseToken, _) = jwtGen.GenerateToken(admin);
            using var enrollReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/enroll");
            enrollReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
            var enrollResp = await _client.SendAsync(enrollReq);
            enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var enrollDto = await enrollResp.Content.ReadFromJsonAsync<EnrollMfaResponseDto>();
            var recovery = enrollDto!.RecoveryCodes[0];

            // Usar código de recuperación una vez funciona y activa MFA
            using var recReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
            {
                Content = JsonContent.Create(new { recoveryCode = recovery })
            };
            recReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
            var recResp1 = await _client.SendAsync(recReq1);
            recResp1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Reusar el mismo código de recuperación falla (401)
            using var recReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
            {
                Content = JsonContent.Create(new { recoveryCode = recovery })
            };
            recReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
            var recResp2 = await _client.SendAsync(recReq2);
            recResp2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Otros 4 intentos inválidos (5 en total) disparan bloqueo temporal (403)
            HttpStatusCode lastStatus = recResp2.StatusCode;
            for (int i = 0; i < 4; i++)
            {
                using var badReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
                {
                    Content = JsonContent.Create(new { code = "000000" })
                };
                badReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseToken);
                var badResp = await _client.SendAsync(badReq);
                lastStatus = badResp.StatusCode;
                if (lastStatus is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    break;
                }
            }

            lastStatus.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests);
        }
        finally
        {
            _factory.FakeClock.Set(DateTime.UtcNow);
        }
    }
}
