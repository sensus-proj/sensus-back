using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Security;

/// <summary>
/// Pruebas de integración de CORS y HTTPS por entorno (Spec 012 / FR-012-01..04 / T012-05 / CA-012-01..02).
/// </summary>
public sealed class CorsAndHttpsSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CorsAndHttpsSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreflightOptions_FromAllowedOrigin_IncludesAccessControlAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", "https://sensus.app");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("https://sensus.app");
    }

    [Fact]
    public async Task PreflightOptions_FromDisallowedOrigin_DoesNotIncludeAccessControlAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", "https://untrusted-external-site.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task DevelopmentEnvironment_HttpsRequest_DoesNotIncludeHstsHeader()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/health/live");

        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task ProductionEnvironment_HttpRequest_RedirectsToHttps()
    {
        using var prodFactory = new ProductionSecurityWebApplicationFactory();
        using var client = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/health/live");

        ((int)response.StatusCode).Should().BeOneOf((int)HttpStatusCode.TemporaryRedirect, (int)HttpStatusCode.PermanentRedirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task ProductionEnvironment_HttpsRequest_IncludesStrictTransportSecurityHeader()
    {
        using var prodFactory = new ProductionSecurityWebApplicationFactory();
        using var client = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/health/live");

        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task ProductionEnvironment_DoesNotExposeApiDocs(string path)
    {
        // FR-011-05 (G-15): la superficie de la API no se publica fuera de Development.
        using var prodFactory = new ProductionSecurityWebApplicationFactory();
        using var client = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Development", false, true, true, false, false)]
    [InlineData("Development", true, true, true, false, false)]
    [InlineData("Production", false, false, false, true, true)]
    [InlineData("Production", true, false, true, true, false)]
    public void HostingSecurityPolicy_ResolvesPipelineByEnvironmentAndLambda(
        string environmentName,
        bool isLambdaRuntime,
        bool exposeDocs,
        bool seedAdmin,
        bool useHsts,
        bool useHttpsRedirection)
    {
        // Regresión: en Lambda (Production) antes se publicaba Swagger y no se aplicaba HSTS.
        var environment = new StubHostEnvironment(environmentName);

        Sensus.Api.Extensions.HostingSecurityPolicy.ExposeApiDocs(environment).Should().Be(exposeDocs);
        Sensus.Api.Extensions.HostingSecurityPolicy.SeedAdministrator(environment, isLambdaRuntime).Should().Be(seedAdmin);
        Sensus.Api.Extensions.HostingSecurityPolicy.UseHsts(environment).Should().Be(useHsts);
        Sensus.Api.Extensions.HostingSecurityPolicy.UseHttpsRedirection(environment, isLambdaRuntime).Should().Be(useHttpsRedirection);
    }

    private sealed class StubHostEnvironment(string environmentName) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Sensus.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class ProductionSecurityWebApplicationFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = Convert.ToBase64String(new byte[64]),
                    ["SesEmailSettings:Simulate"] = "true",
                    ["Cors:AllowedOrigins:0"] = "https://sensus.app",
                    ["Cors:AllowedOrigins:1"] = "https://admin.sensus.app"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
                {
                    options.HttpsPort = 443;
                    options.RedirectStatusCode = (int)System.Net.HttpStatusCode.PermanentRedirect;
                });

                services.Configure<Microsoft.AspNetCore.HttpsPolicy.HstsOptions>(options =>
                {
                    options.ExcludedHosts.Clear();
                    options.MaxAge = TimeSpan.FromDays(365);
                    options.IncludeSubDomains = true;
                });
            });
        }
    }
}
