using Amazon;
using Amazon.DynamoDBv2;
using Amazon.SimpleEmail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Ai.Budget;
using Sensus.Infrastructure.Ai.Gateways;
using Sensus.Infrastructure.Ai.Guardrails;
using Sensus.Infrastructure.Ai.Mcp;
using Sensus.Infrastructure.Ai.Routing;
using Sensus.Infrastructure.Authentication;
using Sensus.Infrastructure.Cache;
using Sensus.Infrastructure.Migration;
using Sensus.Infrastructure.Observability;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Sensus.Infrastructure.Security;
using Sensus.Infrastructure.Services;
using StackExchange.Redis;

namespace Sensus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Servicios base
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Métricas de aplicación en CloudWatch con Embedded Metric Format (Spec 015, FR-015-01)
        services.Configure<MetricsSettings>(configuration.GetSection(MetricsSettings.SectionName));
        services.AddSingleton<IRequestMetricsPublisher>(sp =>
        {
            IOptions<MetricsSettings> metricsSettings = sp.GetRequiredService<IOptions<MetricsSettings>>();
            return metricsSettings.Value.Enabled
                ? new EmbeddedMetricsPublisher(metricsSettings, sp.GetRequiredService<IDateTimeProvider>())
                : new NoOpRequestMetricsPublisher();
        });
        services.AddSingleton<IAiMetricsPublisher>(sp =>
        {
            IOptions<MetricsSettings> metricsSettings = sp.GetRequiredService<IOptions<MetricsSettings>>();
            return metricsSettings.Value.Enabled
                ? new EmbeddedAiMetricsPublisher(metricsSettings, sp.GetRequiredService<IDateTimeProvider>())
                : new NullAiMetricsPublisher();
        });

        // Autenticación, BCrypt y JWT
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddOptions<RefreshTokenSettings>()
            .Bind(configuration.GetSection(RefreshTokenSettings.SectionName))
            .Validate(s => s.RefreshTokenDays is >= 1 and <= 90, "JwtSettings:RefreshTokenDays debe estar entre 1 y 90 días.")
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();

        // Segundo factor TOTP Administrador (Spec 062, G-05)
        services.AddOptions<MfaSettings>()
            .Bind(configuration.GetSection(MfaSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<ITotpService, Rfc6238TotpService>();

        // Geocodificación (SE-003)
        services.AddScoped<IGeocodingService, NominatimGeocodingService>();

        // Servicio de correo transaccional (Spec 029): Amazon SES sobre TLS 1.2+.
        services.AddSingleton<IValidateOptions<SesEmailSettings>, SesEmailSettingsValidator>();
        services.AddOptions<SesEmailSettings>()
            .Bind(configuration.GetSection(SesEmailSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IAmazonSimpleEmailService>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<SesEmailSettings>>().Value;
            var clientConfig = new AmazonSimpleEmailServiceConfig();
            var region = settings.GetRegionEndpoint();
            if (region is not null)
            {
                clientConfig.RegionEndpoint = region;
            }

            return new AmazonSimpleEmailServiceClient(clientConfig);
        });

        services.AddSingleton<IEmailSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SesEmailSettings>>();
            var logger = provider.GetRequiredService<ILogger<SesEmailSender>>();
            var client = options.Value.Simulate
                ? null
                : provider.GetRequiredService<IAmazonSimpleEmailService>();

            return new SesEmailSender(options, client, logger);
        });

        // Almacenamiento y firma de fotos Cloudinary (SE-006 / FR-003-06)
        services.Configure<CloudinarySettings>(
            configuration.GetSection(CloudinarySettings.SectionName));
        services.Configure<CloudinaryAccountSettings>(
            configuration.GetSection(CloudinaryAccountSettings.SectionName));
        services.AddSingleton<IProfileImageStorage, CloudinaryImageStorage>();

        // Moderación inteligente de imágenes y texto con OpenAI
        services.AddSingleton<IValidateOptions<OpenAiSettings>, OpenAiSettingsValidator>();
        services.AddOptions<OpenAiSettings>()
            .Bind(configuration.GetSection(OpenAiSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SensiSettings>, SensiSettingsValidator>();
        services.AddOptions<SensiSettings>()
            .Bind(configuration.GetSection(SensiSettings.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IImageModerationService, OpenAiImageModerationService>();
        services.AddHttpClient<IPublicationTextModerationService, OpenAiImageModerationService>();
        services.AddHttpClient<ITextModerationService, OpenAiTextModerationService>();

        // Base de conocimiento estática RAG para Sensi IA (Spec 046)
        services.AddSingleton<IKnowledgeBaseProvider, Knowledge.KnowledgeBaseProvider>();

        // Sensi IA (Spec 046) - Componentes de Capa 1 a 7
        services.AddSingleton<IScopeAuthorizationMapper, JwtScopeAuthorizationMapper>();
        services.AddSingleton<IMcpToolRegistry, McpToolRegistry>();
        services.AddScoped<IMcpToolExecutor, InProcessMcpToolExecutor>();
        services.AddSingleton<IPromptInjectionDetector, RegexAndStructuralInjectionDetector>();
        services.AddSingleton<IHardRuleLeakDetector, HardRuleLeakDetector>();
        services.AddSingleton<IPiiRedactor, PatternPiiAndSecretRedactor>();
        services.AddSingleton<IAiModelRouter, DeterministicModelRouter>();
        // Contadores atómicos de presupuesto y cuota de Sensi IA en Amazon DynamoDB (Spec 049, ADR 0013)
        services.Configure<AiBudgetStoreSettings>(
            configuration.GetSection(AiBudgetStoreSettings.SectionName));
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var storeSettings = sp.GetRequiredService<IOptions<AiBudgetStoreSettings>>().Value;
            var timeout = TimeSpan.FromSeconds(storeSettings.TimeoutSeconds > 0 ? storeSettings.TimeoutSeconds : 3);
            var clientConfig = new AmazonDynamoDBConfig
            {
                Timeout = timeout,
                MaxErrorRetry = 1
            };

            if (!string.IsNullOrWhiteSpace(storeSettings.ServiceUrl))
            {
                clientConfig.ServiceURL = storeSettings.ServiceUrl;
            }
            else
            {
                var region = System.Environment.GetEnvironmentVariable("AWS_REGION")
                    ?? System.Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
                    ?? "us-east-2";
                clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            }

            return new AmazonDynamoDBClient(clientConfig);
        });
        services.AddSingleton<IBudgetCounterStore, DynamoDbBudgetCounterStore>();
        services.AddSingleton<IAiBudgetGuard, AiBudgetGuard>();
        services.AddHttpClient<IAiChatGateway, OpenAiChatGateway>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<OpenAiSettings>>().Value;
            var baseUrl = !string.IsNullOrWhiteSpace(opts.BaseUrl) ? opts.BaseUrl.TrimEnd('/') + "/" : "https://api.openai.com/v1/";
            client.BaseAddress = new Uri(baseUrl);
            var timeout = opts.Sensi.TimeoutSeconds > 0 ? opts.Sensi.TimeoutSeconds : 25;
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });

        services.AddScoped<IAiConversationRepository, MongoAiConversationRepository>();
        services.AddScoped<IAiMessageRepository, MongoAiMessageRepository>();
        services.AddScoped<IAiTraceLogRepository, MongoAiTraceLogRepository>();

        // Caché distribuido con Redis (Spec 048)
        services.Configure<RedisSettings>(
            configuration.GetSection(RedisSettings.SectionName));

        var redisSection = configuration.GetSection(RedisSettings.SectionName);
        var redisConn = redisSection["ConnectionString"];
        var redisEnabled = !bool.TryParse(redisSection["Enabled"], out var enabledVal) || enabledVal;

        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConn);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 2000;
                return ConnectionMultiplexer.Connect(options);
            });
        }

        // Spec 048: con Redis habilitado y configurado se usa Redis; si no (despliegue en Lambda sin ElastiCache,
        // por costo) se usa la caché en memoria de proceso, con TTL acotado, para que las lecturas sigan cacheadas.
        services.Configure<MemoryCacheSettings>(configuration.GetSection(MemoryCacheSettings.SectionName));
        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        // Configuración de MongoDB Atlas
        services.Configure<MongoDbSettings>(
            configuration.GetSection(MongoDbSettings.SectionName));
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IComercioRepository, MongoComercioRepository>();
        services.AddScoped<IPasswordResetTokenRepository, MongoPasswordResetTokenRepository>();
        services.AddScoped<IOfferRepository, MongoOfferRepository>();
        services.AddScoped<IOfferClaimRepository, MongoOfferClaimRepository>();
        services.AddScoped<IOfferViewRepository, MongoOfferViewRepository>();

        // Firma HMAC de códigos de apartado (spec 033)
        services.Configure<ClaimCodeSettings>(
            configuration.GetSection(ClaimCodeSettings.SectionName));
        services.AddSingleton<IClaimCodeService, HmacClaimCodeService>();

        // Almacenamiento multimedia en Cloudinary (Spec 022)
        services.AddSingleton<IMediaStorage, CloudinaryMediaStorage>();
        services.AddScoped<IMediaAssetRepository, MongoMediaAssetRepository>();

        // Auditoría de moderación append-only (Spec 021)
        services.Configure<ModerationAuditSettings>(
            configuration.GetSection(ModerationAuditSettings.SectionName));
        services.AddScoped<IModerationAuditRepository, MongoModerationAuditRepository>();

        // Entrega de contenido por CDN con URLs firmadas (Spec 023)
        services.Configure<MediaDeliverySettings>(
            configuration.GetSection(MediaDeliverySettings.SectionName));
        services.AddSingleton<IMediaDeliveryUrlGenerator, HmacMediaDeliveryUrlGenerator>();
        services.AddSingleton<ICdnCacheInvalidator, CloudinaryCdnCacheInvalidator>();
        services.AddHttpClient<IMediaContentProxy, CloudinaryMediaContentProxy>();

        // Registro de dispositivos para push (Spec 027)
        services.AddScoped<IDeviceRepository, MongoDeviceRepository>();
        services.AddScoped<IFriendRequestRepository, MongoFriendRequestRepository>();
        services.AddScoped<ISharedOfferRepository, MongoSharedOfferRepository>();
        services.AddScoped<IOfferLikeRepository, MongoOfferLikeRepository>();
        services.AddScoped<IOfferFavoriteRepository, MongoOfferFavoriteRepository>();
        services.AddScoped<IStoreReviewRepository, MongoStoreReviewRepository>();
        services.AddScoped<IFeedPostRepository, MongoFeedPostRepository>();
        services.AddScoped<IFeedCommentRepository, MongoFeedCommentRepository>();
        services.AddScoped<IFeedPostVisibilityLookup, MongoFeedPostVisibilityLookup>();
        services.AddScoped<IFeedPostLikeRepository, MongoFeedPostLikeRepository>();
        services.Configure<ContentReportSettings>(
            configuration.GetSection(ContentReportSettings.SectionName));
        services.AddScoped<IContentReportRepository, MongoContentReportRepository>();
        services.AddScoped<IReportTargetService, MongoReportTargetService>();
        services.AddScoped<IOfferStatusHistoryRepository, MongoOfferStatusHistoryRepository>();

        // Sanciones y baneo (Spec 020): configuración, persistencia y métricas
        services.Configure<Sensus.Application.Common.Settings.SanctionSettings>(
            configuration.GetSection(Sensus.Application.Common.Settings.SanctionSettings.SectionName));
        services.AddScoped<Sensus.Domain.Repositories.ISanctionRepository, MongoSanctionRepository>();
        services.AddScoped<Sensus.Domain.Repositories.IModerationAuditRepository, MongoModerationAuditRepository>();

        // Baja de cuenta, purga diferida y auditoría anonimizada (Specs 060 y 061)
        services.Configure<Sensus.Application.Common.Settings.AccountDeletionSettings>(
            configuration.GetSection(Sensus.Application.Common.Settings.AccountDeletionSettings.SectionName));
        services.AddSingleton<ISanctionMetrics>(sp =>
        {
            IOptions<MetricsSettings> metricsSettings = sp.GetRequiredService<IOptions<MetricsSettings>>();
            return metricsSettings.Value.Enabled
                ? new EmbeddedSanctionMetrics(metricsSettings, sp.GetRequiredService<IDateTimeProvider>())
                : new NullSanctionMetrics();
        });

        // Preferencias de notificación (Spec 044)
        services.AddScoped<INotificationSettingsRepository, MongoNotificationSettingsRepository>();

        // Intereses del usuario (Spec 054)
        services.AddScoped<IUserInterestsRepository, MongoUserInterestsRepository>();

        // Registro seudonimizado de interacciones y métricas en Atlas M0 (Spec 055)
        services.Configure<InteractionSettings>(
            configuration.GetSection(InteractionSettings.SectionName));
        services.AddSingleton<IUserPseudonymizer, HmacUserPseudonymizer>();
        services.AddScoped<IOfferInteractionRepository, MongoOfferInteractionRepository>();
        services.AddSingleton<IInteractionMetricsPublisher>(sp =>
        {
            IOptions<MetricsSettings> metricsSettings = sp.GetRequiredService<IOptions<MetricsSettings>>();
            return metricsSettings.Value.Enabled
                ? new EmbeddedInteractionMetricsPublisher(
                    metricsSettings,
                    sp.GetRequiredService<IOptions<InteractionSettings>>(),
                    sp.GetRequiredService<IDateTimeProvider>(),
                    sp.GetRequiredService<ILogger<EmbeddedInteractionMetricsPublisher>>())
                : new NullInteractionMetricsPublisher();
        });
        services.AddScoped<IOfferInteractionRecorder, MongoOfferInteractionRecorder>();
        services.AddScoped<InteractionMetricsAggregationService>();

        // Feed de promociones por intereses (Spec 042)
        services.Configure<Sensus.Application.Common.Settings.FeedRankingSettings>(
            configuration.GetSection(Sensus.Application.Common.Settings.FeedRankingSettings.SectionName));
        services.AddScoped<IUserAffinityReader, MongoUserAffinityReader>();
        services.AddScoped<IFeedSnapshotStore, RedisFeedSnapshotStore>();
        services.AddSingleton<IFeedMetricsPublisher, StructuredFeedMetricsPublisher>();

        // Servicio de migración de SQL a MongoDB Atlas y migración de categorías (FR-034-09) y límite activo (FR-034-05)
        services.AddOptions<Sensus.Application.Common.Settings.OfferLimitSettings>()
            .Bind(configuration.GetSection(Sensus.Application.Common.Settings.OfferLimitSettings.SectionName))
            .Validate(
                s => s.MaxActivePerMerchant is >= 1 and <= 1000,
                "Offers:MaxActivePerMerchant must be between 1 and 1000.")
            .ValidateOnStart();
        services.AddOptions<Sensus.Application.Common.Settings.OfferModerationRetrySettings>()
            .Bind(configuration.GetSection(Sensus.Application.Common.Settings.OfferModerationRetrySettings.SectionName))
            .Validate(
                s => s.MaxAttempts is >= 1 and <= 50
                    && s.RetryDelaySeconds is >= 5 and <= 3600
                    && s.CheckIntervalSeconds is >= 5 and <= 3600
                    && s.BatchSize is >= 1 and <= 100,
                "Moderation:OfferRetry settings are out of allowed range.")
            .ValidateOnStart();
        services.Configure<Sensus.Application.Common.Settings.OfferCategorySettings>(
            configuration.GetSection(Sensus.Application.Common.Settings.OfferCategorySettings.SectionName));
        services.AddScoped<SqlToMongoMigrationService>();
        services.AddScoped<OfferCategoryMigrationService>();
        services.AddScoped<IOfferCategoryMigration>(sp => sp.GetRequiredService<OfferCategoryMigrationService>());

        return services;
    }
}
