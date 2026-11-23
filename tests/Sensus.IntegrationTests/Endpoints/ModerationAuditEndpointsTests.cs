using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Moderation.Audit;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas de integración para GET /api/v1/admin/moderation/audit (Spec 021).
/// Ref: FR-021-03 - consulta paginada por administrador; CA-021-03 - 403 sin permisos.
/// </summary>
public sealed class ModerationAuditEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ModerationAuditEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string CreateToken(string role)
    {
        var user = User.Create(
            "Usuario Auditor",
            $"audit-{Guid.NewGuid():N}@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        if (role != RoleType.Consumidor)
        {
            user.AddRole(role);
        }

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    [Fact]
    public async Task GetAudit_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Ref: CA-021-03 / Principio IV - 401 sin autenticación
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(RoleType.Consumidor)]
    [InlineData(RoleType.Comerciante)]
    public async Task GetAudit_WhenRoleIsNotAdmin_ReturnsForbidden(string role)
    {
        // Ref: CA-021-03 - 403 para usuarios no administradores
        string token = CreateToken(role);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAudit_WhenCallerIsAdmin_ReturnsOkWithPagedResult()
    {
        // Ref: FR-021-03 - 200 OK con resultados paginados para administradores
        string token = CreateToken(RoleType.Administrador);

        var record = ModerationAuditRecord.Create(
            Guid.NewGuid(),
            ModerationTargetType.Offer,
            ModerationVerdict.Approved,
            "Approved",
            "offer_create",
            ["safe"],
            new Dictionary<string, double> { ["safe"] = 0.05 },
            "threshold_ok",
            ModerationActorType.User,
            "user-1",
            "corr-1",
            Now,
            365).Value;

        _factory.ModerationAuditRepository
            .ListAsync(
                Arg.Any<Guid?>(),
                Arg.Any<ModerationTargetType?>(),
                Arg.Any<ModerationVerdict?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(([record], 1L));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/moderation/audit?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<ModerationAuditRecordDto>>();

        pagedResult.Should().NotBeNull();
        pagedResult!.Items.Should().HaveCount(1);
        pagedResult.TotalCount.Should().Be(1);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(20);

        ModerationAuditRecordDto item = pagedResult.Items[0];
        item.Id.Should().Be(record.Id);
        item.TargetId.Should().Be(record.TargetId);
        item.TargetType.Should().Be("Offer");
        item.Verdict.Should().Be("Approved");
        item.Decision.Should().Be("Approved");
        item.ContentContext.Should().Be("offer_create");
        item.FlaggedCategories.Should().Equal("safe");
        item.ActorType.Should().Be("User");
        item.CorrelationId.Should().Be("corr-1");
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?targetType=NonExistentType")]
    [InlineData("?verdict=InvalidVerdict")]
    public async Task GetAudit_WithInvalidQueryParams_ReturnsBadRequest(string queryString)
    {
        // Ref: FR-021-03 - validación estricta de parámetros
        string token = CreateToken(RoleType.Administrador);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/moderation/audit{queryString}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
