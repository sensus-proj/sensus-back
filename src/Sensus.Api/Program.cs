using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amazon.SimpleSystemsManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Sensus.Api.Endpoints;
using Sensus.Api.Extensions;
using Sensus.Api.Middlewares;
using Sensus.Api.Services;
using Sensus.Application;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Notifications;
using Sensus.Application.Users.Commands.NotificationLocation;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure;
using Sensus.Infrastructure.Authentication;
using Sensus.Infrastructure.Migration;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Sensus.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Carga opcional de configuración local privada (ignorada en Git) para credenciales de MongoDB Atlas y llaves locales
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Secretos de producción desde SSM Parameter Store (Principio VIII, regla 5). Va al final: tiene la mayor prioridad.
builder.Configuration.AddSensusSecretStore(Environment.GetEnvironmentVariable(SecretsConfigurationExtensions.PrefixVariable));

// El secreto JWT nunca se versiona (AGENTS.md §8). Solo en Development y Testing se genera uno local cuando
// no está configurado. En Lambda su ausencia detiene el arranque (fail-closed): un secreto derivable del código
// permitiría a cualquiera fabricar tokens válidos.
var configuredSecret = builder.Configuration["JwtSettings:Secret"];
var isLambdaRuntime = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
if (string.IsNullOrWhiteSpace(configuredSecret) && (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
{
    var seedBytes = Encoding.UTF8.GetBytes($"Sensus.JwtKey.Deterministic.{builder.Environment.EnvironmentName}");
    configuredSecret = Convert.ToBase64String(SHA384.HashData(seedBytes));
    builder.Configuration["JwtSettings:Secret"] = configuredSecret;
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["JwtSettings:Secret"] = configuredSecret
    });
}

// 1. Capa de Aplicación
builder.Services.AddApplication();

// 2. Capa de Infraestructura (BCrypt, JWT, MongoDB Atlas)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IClaimPushNotifier, ClaimPushNotifier>();
builder.Services.AddScoped<IClaimPushReceiptStore, MongoClaimPushReceiptStore>();
builder.Services.AddScoped<INearbyOfferNotifier, NearbyOfferNotifier>();
builder.Services.AddOptions<NearbyOfferAlertSettings>()
    .BindConfiguration(NearbyOfferAlertSettings.SectionName)
    .Validate(s => s.TimeoutSeconds is >= 1 and <= 10, "NearbyOfferAlerts:TimeoutSeconds must be between 1 and 10.")
    .Validate(s => s.MaxRadiusKm is >= 1 and <= 50, "NearbyOfferAlerts:MaxRadiusKm must be between 1 and 50.")
    .Validate(s => s.MaxRecipients is >= 1 and <= 1000, "NearbyOfferAlerts:MaxRecipients must be between 1 and 1000.")
    .Validate(s => s.MaxParallelSends is >= 1 and <= 32, "NearbyOfferAlerts:MaxParallelSends must be between 1 and 32.")
    .Validate(s => s.MaxTitleLength is >= 10 and <= 120, "NearbyOfferAlerts:MaxTitleLength must be between 10 and 120.")
    .ValidateOnStart();
builder.Services.AddScoped<INotificationLocationRepository, MongoNotificationLocationRepository>();
builder.Services.AddOptions<NotificationLocationSettings>()
    .BindConfiguration("NotificationLocation")
    .Validate(settings => settings.LifetimeHours is > 0 and <= 24, "Location lifetime must be between 1 and 24 hours.")
    .ValidateOnStart();
builder.Services.AddOptions<Sensus.Application.Common.Settings.OfferSearchSettings>()
    .BindConfiguration(Sensus.Application.Common.Settings.OfferSearchSettings.SectionName)
    .Validate(s => s.DefaultRadiusKm is >= 1d and <= 50d, "OfferSearch:DefaultRadiusKm must be between 1 and 50.")
    .Validate(s => s.MaxRadiusKm is >= 1d and <= 50d, "OfferSearch:MaxRadiusKm must be between 1 and 50.")
    .Validate(s => s.GuestMaxRadiusKm is >= 1d and <= 2d, "OfferSearch:GuestMaxRadiusKm must be between 1 and 2.")
    .Validate(s => s.MaxCandidates is >= 10 and <= 500, "OfferSearch:MaxCandidates must be between 10 and 500.")
    .Validate(s => s.Weights.Text >= 0 && s.Weights.CategoryMatch >= 0 && s.Weights.Proximity >= 0 && s.Weights.Popularity >= 0 &&
                   (s.Weights.Text + s.Weights.CategoryMatch + s.Weights.Proximity + s.Weights.Popularity) > 0d,
        "OfferSearch:Weights must be non-negative and sum above 0.")
    .ValidateOnStart();
builder.Services.AddOptions<Sensus.Application.Common.Settings.OfferAlternativesSettings>()
    .BindConfiguration(Sensus.Application.Common.Settings.OfferAlternativesSettings.SectionName)
    .Validate(s => s.DefaultLimit is >= 1 and <= 10, "OfferAlternatives:DefaultLimit must be between 1 and 10.")
    .Validate(s => s.MaxLimit is >= 1 and <= 10, "OfferAlternatives:MaxLimit must be between 1 and 10.")
    .Validate(s => s.RegisteredMaxDistanceMeters is >= 100 and <= 50000, "OfferAlternatives:RegisteredMaxDistanceMeters must be between 100 and 50000.")
    .Validate(s => s.GuestMaxDistanceMeters is >= 100 and <= 2000, "OfferAlternatives:GuestMaxDistanceMeters must be between 100 and 2000.")
    .Validate(s => s.ComparablePriceRatio is > 0m and <= 5.0m, "OfferAlternatives:ComparablePriceRatio must be between 0 and 5.0.")
    .ValidateOnStart();
bool allowSimulatedPush = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
if (allowSimulatedPush && builder.Configuration.GetValue("Push:Simulate", true))
{
    builder.Services.AddSingleton<IPushDispatcher, SimulatedPushDispatcher>();
}
else
{
    builder.Services.AddSingleton<IAmazonSimpleSystemsManagement>(_ => new AmazonSimpleSystemsManagementClient());
    builder.Services.AddSingleton<FcmCredentials>();
    builder.Services.AddHttpClient<IPushDispatcher, FcmPushDispatcher>(client =>
    {
        client.BaseAddress = new Uri("https://fcm.googleapis.com/");
    }).RemoveAllLoggers();
}

// 3. Capa de Presentación / API
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

// Configuración de Autenticación con JWT Bearer y validación de SecurityStamp
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.Secret) && !string.IsNullOrWhiteSpace(configuredSecret))
{
    jwtSettings.Secret = configuredSecret;
}

var secretKey = !string.IsNullOrWhiteSpace(jwtSettings.Secret) ? Encoding.UTF8.GetBytes(jwtSettings.Secret) : RandomNumberGenerator.GetBytes(48);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userPrincipal = context.Principal;
            if (userPrincipal is null)
            {
                context.Fail("Principal no disponible.");
                return;
            }

            var isGuest = userPrincipal.IsInRole(RoleType.Invitado);
            if (isGuest)
            {
                // Las sesiones de invitado son efímeras y no requieren validación contra BD
                return;
            }

            var userIdString = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                               userPrincipal.FindFirstValue("sub");

            if (!Guid.TryParse(userIdString, out var userId))
            {
                context.Fail("Identificador de usuario inválido en token.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByIdAsync(userId, context.HttpContext.RequestAborted);

            if (user is null || !user.Estatus)
            {
                context.Fail("El usuario no existe o se encuentra inactivo.");
                return;
            }

            // Spec 020 (FR-020-04, NFR-020-01): una sanción vigente invalida de inmediato los tokens ya emitidos.
            var clock = context.HttpContext.RequestServices.GetRequiredService<IDateTimeProvider>();
            if (user.IsSuspended(clock.UtcNow))
            {
                context.Fail("La cuenta se encuentra suspendida por una sanción.");
                return;
            }

            var tokenSecurityStamp = userPrincipal.FindFirstValue(JwtTokenGenerator.SecurityStampClaimType);
            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            var isLogoutPath = path.EndsWith("/auth/logout", StringComparison.OrdinalIgnoreCase);

            if (!isLogoutPath && tokenSecurityStamp is not null && !string.Equals(tokenSecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
            {
                context.Fail("La sesión ha sido revocada o la contraseña fue restablecida.");
            }
        }
    };
});

// Políticas de Autorización y Rate Limiting
builder.Services.AddAppAuthorizationPolicies();
builder.Services.AddSecurityRateLimiting(builder.Configuration);
builder.Services.AddHostedService<OfferExpirationBackgroundService>();
builder.Services.AddHostedService<OfferClaimExpirationBackgroundService>();
builder.Services.AddHostedService<QuarantineRetentionBackgroundService>();
builder.Services.AddHostedService<InteractionStatsBackgroundService>();
builder.Services.AddHostedService<OfferCategoryMigrationHostedService>();

// Manejo global de excepciones con ProblemDetails (RFC 7807)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Documentación Swagger y OpenAPI con soporte para JWT Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sensus API",
        Version = "v1",
        Description = "API backend empresarial de Sensus con Clean Architecture, CQRS, MongoDB Atlas y autenticación JWT."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Pega aquí tu token JWT obtenido en /login",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});
builder.Services.AddOpenApi();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<Sensus.Api.Health.MongoReadinessHealthCheck>("mongodb", tags: ["ready"])
    .AddCheck<Sensus.Api.Health.CacheHealthCheck>("cache", tags: ["ready"]);

// Configuración de CORS segura con allowlist explícita (nunca AllowAnyOrigin)
const string SensusCorsPolicy = "SensusCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(SensusCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Pipeline HTTP
app.UseMiddleware<Sensus.Api.Middlewares.CorrelationIdMiddleware>();
// FR-015-01: las métricas envuelven todo el pipeline para medir la latencia completa y el estado final.
app.UseMiddleware<Sensus.Api.Middlewares.RequestMetricsMiddleware>();
app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseCors(SensusCorsPolicy);
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || isLambdaRuntime)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sensus API v1");
        c.RoutePrefix = "swagger";
    });
    app.MapOpenApi();

    // Sembrado del usuario Administrador (si se configuran variables de entorno)
    using var scope = app.Services.CreateScope();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AdminUserSeeder.SeedAdminUserAsync(userRepository, passwordHasher, app.Configuration, logger);
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoint de verificación de salud
// FR-016-01: liveness (sin dependencias externas) y /health como alias compatible.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
// FR-016-02: readiness; responde 503 si MongoDB no está disponible.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Registro de endpoints
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapMerchantEndpoints();
app.MapOfferEndpoints();
app.MapMediaEndpoints();
app.MapSanctionEndpoints();
app.MapAiEndpoints();
app.MapFriendEndpoints();
app.MapFeedEndpoints();
app.MapStoreReviewEndpoints();
app.MapCommunityEndpoints();

await app.RunAsync();

// Requerido para WebApplicationFactory en pruebas
public partial class Program { }
