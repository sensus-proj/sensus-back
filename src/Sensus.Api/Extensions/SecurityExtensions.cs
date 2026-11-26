using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sensus.Domain.Enums;

namespace Sensus.Api.Extensions;

public static class SecurityExtensions
{
    public const string AuthStrictPolicy = "auth-strict";
    public const string RecoverPolicy = "recover";
    public const string RecoverByEmailPolicy = "recover-by-email";

    /// <summary>Clave de <see cref="HttpContext.Items"/> con el hash SHA-256 del correo normalizado.</summary>
    public const string RecoverEmailPartitionKeyItem = "sensus:recover-email-partition-key";
    public const string ReadHeavyPolicy = "read-heavy";
    public const string SensitiveWritePolicy = "sensitive-write";
    public const string FavoritesPolicy = "favorites";
    public const string AiChatPolicy = "ai-chat";
    public const string PostLikesPolicy = "post-likes";

    public const string RegisteredUserPolicy = "RegisteredUser";
    public const string MerchantOnlyPolicy = "MerchantOnly";
    public const string GuestOrRegisteredPolicy = "GuestOrRegistered";
    public const string AdminBasePolicy = "AdminBase";
    public const string MfaVerifyPolicy = "MfaVerify";
    public const string AdminOnlyPolicy = "AdminOnly";

    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var settings = configuration?.GetSection(RateLimitingSettings.SectionName).Get<RateLimitingSettings>()
            ?? new RateLimitingSettings();

        if (configuration is not null)
        {
            services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = (int)Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                }

                var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var cidObj)
                    ? cidObj?.ToString()
                    : context.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                      ?? context.HttpContext.TraceIdentifier;

                var problem = new ProblemDetails
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Se ha excedido el límite de peticiones permitido. Por favor intente más tarde.",
                    Instance = context.HttpContext.Request.Path
                };

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    problem.Extensions["correlationId"] = correlationId;
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: token);
            };

            options.AddPolicy(AuthStrictPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthStrict.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.AuthStrict.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RecoverPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Recover.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Recover.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // NFR-029-03: 3 solicitudes por correo y hora. La clave de partición es el hash SHA-256 del
            // correo normalizado, nunca el correo en claro, y se reutiliza entre IP y entre instancias.
            options.AddPolicy(RecoverByEmailPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRecoverPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Recover.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Recover.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(ReadHeavyPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.ReadHeavy.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.ReadHeavy.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(SensitiveWritePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.SensitiveWrite.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.SensitiveWrite.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // FR-038-06: 120 alternancias por usuario y hora (ventana de una hora).
            options.AddPolicy(FavoritesPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Favorites.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Favorites.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // FR-039-06: 60 alternancias de me gusta por usuario y hora (umbral en configuración).
            options.AddPolicy(PostLikesPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PostLikes.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.PostLikes.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(AiChatPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AiChat.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.AiChat.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static string GetRecoverPartitionKey(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(RecoverEmailPartitionKeyItem, out var partitionKey) &&
        partitionKey is string key
            ? key
            : $"recover:{GetPartitionKey(httpContext)}";

    /// <summary>
    /// NFR-029-03: lee el correo de forma asíncrona antes del rate limiter y guarda solo su hash SHA-256
    /// en <see cref="HttpContext.Items"/>. ASP.NET Core prohíbe las lecturas síncronas del cuerpo, por lo
    /// que el particionador del limitador no puede hacerlo. El cuerpo se rebobina para el model binding.
    /// </summary>
    public static IApplicationBuilder UseRecoverEmailPartitionKey(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method) &&
                context.Request.Path.Equals("/api/v1/auth/recover", StringComparison.OrdinalIgnoreCase))
            {
                await SetRecoverEmailPartitionKeyAsync(context);
            }

            await next();
        });

    private static async Task SetRecoverEmailPartitionKeyAsync(HttpContext context)
    {
        var request = context.Request;

        // Límite de tamaño para no convertir el particionador en un vector de DoS.
        if (request.ContentLength is null or <= 0 || request.ContentLength > 4096)
        {
            return;
        }

        try
        {
            request.EnableBuffering();
            request.Body.Position = 0;

            using var document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: context.RequestAborted);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "correo", StringComparison.OrdinalIgnoreCase) ||
                    property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var email = property.Value.GetString();
                if (string.IsNullOrWhiteSpace(email))
                {
                    continue;
                }

                var normalized = email.Trim().ToLowerInvariant();
                var hash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
                context.Items[RecoverEmailPartitionKeyItem] = $"recover:email:{hash}";
                return;
            }
        }
        catch (JsonException)
        {
            // Cuerpo inválido: se limita por IP y el validador del endpoint devolverá 400.
        }
        catch (IOException)
        {
            // Cuerpo no legible: se limita por IP sin filtrar el error al cliente.
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return $"user:{userId}";
            }
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        }

        return $"ip:{clientIp ?? "unknown"}";
    }

    public static IServiceCollection AddAppAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RegisteredUserPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado) && !IsMfaChallengeToken(ctx.User));
            });

            options.AddPolicy(MerchantOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleType.Comerciante);
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado) && !IsMfaChallengeToken(ctx.User));
            });

            options.AddPolicy(GuestOrRegisteredPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx => !IsMfaChallengeToken(ctx.User));
            });

            // Spec 062: habilita el primer enrolamiento de Administrador antes de contar con amr=mfa.
            options.AddPolicy(AdminBasePolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleType.Administrador);
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado) && !IsMfaChallengeToken(ctx.User));
            });

            // Spec 062: verificación TOTP admite sesión Admin existente o token intermedio mfa_challenge.
            options.AddPolicy(MfaVerifyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado) &&
                    (ctx.User.IsInRole(RoleType.Administrador) || IsMfaChallengeToken(ctx.User)));
            });

            // FR-019-04 & FR-062-05: acciones administrativas reservadas a Administrador con step-up amr=mfa vigente.
            options.AddPolicy(AdminOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleType.Administrador);
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado) &&
                    !IsMfaChallengeToken(ctx.User) &&
                    HasRecentAdminMfaStepUp(ctx));
            });
        });

        return services;
    }

    private static bool IsMfaChallengeToken(System.Security.Claims.ClaimsPrincipal user) =>
        user.HasClaim(c =>
            string.Equals(c.Type, Sensus.Infrastructure.Authentication.JwtTokenGenerator.TokenUseClaimType, StringComparison.Ordinal) &&
            string.Equals(c.Value, Sensus.Infrastructure.Authentication.JwtTokenGenerator.MfaChallengeTokenUse, StringComparison.Ordinal));

    private const string MappedAmrClaimType = "http://schemas.microsoft.com/claims/authnmethodsreferences";

    private static bool HasRecentAdminMfaStepUp(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext ctx)
    {
        var hasAmrMfa = ctx.User.HasClaim(c =>
            (string.Equals(c.Type, Sensus.Infrastructure.Authentication.JwtTokenGenerator.AmrClaimType, StringComparison.Ordinal) ||
             string.Equals(c.Type, MappedAmrClaimType, StringComparison.Ordinal) ||
             string.Equals(c.Type, System.Security.Claims.ClaimTypes.AuthenticationMethod, StringComparison.Ordinal)) &&
            string.Equals(c.Value, "mfa", StringComparison.Ordinal));

        if (!hasAmrMfa)
        {
            return false;
        }

        var mfaAtRaw = ctx.User.FindFirst(Sensus.Infrastructure.Authentication.JwtTokenGenerator.MfaVerifiedAtClaimType)?.Value;
        if (!long.TryParse(mfaAtRaw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long mfaAtUnix))
        {
            return false;
        }

        DateTime verifiedAtUtc = DateTimeOffset.FromUnixTimeSeconds(mfaAtUnix).UtcDateTime;
        DateTime nowUtc = DateTime.UtcNow;
        int windowMinutes = 15;

        if (ctx.Resource is HttpContext httpContext)
        {
            var clock = httpContext.RequestServices.GetService<Sensus.Application.Common.Interfaces.IDateTimeProvider>();
            if (clock is not null)
            {
                nowUtc = clock.UtcNow;
            }

            var mfaOptions = httpContext.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<Sensus.Application.Common.Settings.MfaSettings>>();
            if (mfaOptions?.Value.StepUpWindowMinutes is > 0)
            {
                windowMinutes = mfaOptions.Value.StepUpWindowMinutes;
            }
        }

        if (verifiedAtUtc > nowUtc.AddMinutes(1))
        {
            return false;
        }

        return (nowUtc - verifiedAtUtc) <= TimeSpan.FromMinutes(windowMinutes);
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            await next();
        });
    }
}
