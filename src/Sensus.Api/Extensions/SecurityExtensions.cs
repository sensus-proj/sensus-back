using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sensus.Domain.Enums;

namespace Sensus.Api.Extensions;

public static class SecurityExtensions
{
    public const string AuthStrictPolicy = "auth-strict";
    public const string RecoverPolicy = "recover";
    public const string ReadHeavyPolicy = "read-heavy";
    public const string SensitiveWritePolicy = "sensitive-write";
    public const string AiChatPolicy = "ai-chat";

    public const string RegisteredUserPolicy = "RegisteredUser";
    public const string MerchantOnlyPolicy = "MerchantOnly";
    public const string GuestOrRegisteredPolicy = "GuestOrRegistered";
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
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Se ha excedido el límite de peticiones permitido. Por favor intente más tarde.",
                    Instance = context.HttpContext.Request.Path
                };

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    problem.Extensions["correlationId"] = correlationId;
                }

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: token);
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
                    !ctx.User.IsInRole(RoleType.Invitado));
            });

            options.AddPolicy(MerchantOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleType.Comerciante);
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado));
            });

            options.AddPolicy(GuestOrRegisteredPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            // FR-019-04: acciones de moderación manual reservadas al rol Administrador.
            options.AddPolicy(AdminOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleType.Administrador);
                policy.RequireAssertion(ctx =>
                    !ctx.User.IsInRole(RoleType.Invitado));
            });
        });

        return services;
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
