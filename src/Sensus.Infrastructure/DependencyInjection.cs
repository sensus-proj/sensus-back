using Amazon;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // Autenticación, BCrypt y JWT
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Geocodificación (SE-003)
        services.AddScoped<IGeocodingService, NominatimGeocodingService>();

        // Servicio de correo transaccional (SE-005 / Api.Mailer.Invitation)
        services.Configure<MailerSettings>(
            configuration.GetSection(MailerSettings.SectionName));
        services.AddHttpClient<IEmailSender, HttpMailerEmailSender>();

        // Almacenamiento y firma de fotos Cloudinary (SE-006)
        services.Configure<CloudinarySettings>(
            configuration.GetSection(CloudinarySettings.SectionName));
        services.AddSingleton<IProfileImageStorage, CloudinaryImageStorage>();

        // Moderación inteligente de imágenes y texto con OpenAI
        services.Configure<OpenAiSettings>(
            configuration.GetSection(OpenAiSettings.SectionName));
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

        // Registro de dispositivos para push (Spec 027)
        services.AddScoped<IDeviceRepository, MongoDeviceRepository>();
        services.AddScoped<IFriendRequestRepository, MongoFriendRequestRepository>();
        services.AddScoped<ISharedOfferRepository, MongoSharedOfferRepository>();

        // Sanciones y baneo (Spec 020): configuración, persistencia y métricas
        services.Configure<Sensus.Application.Common.Settings.SanctionSettings>(
            configuration.GetSection(Sensus.Application.Common.Settings.SanctionSettings.SectionName));
        services.AddScoped<Sensus.Domain.Repositories.ISanctionRepository, MongoSanctionRepository>();
        services.AddSingleton<ISanctionMetrics>(sp =>
        {
            IOptions<MetricsSettings> metricsSettings = sp.GetRequiredService<IOptions<MetricsSettings>>();
            return metricsSettings.Value.Enabled
                ? new EmbeddedSanctionMetrics(metricsSettings, sp.GetRequiredService<IDateTimeProvider>())
                : new NullSanctionMetrics();
        });

        // Preferencias de notificación (Spec 044)
        services.AddScoped<INotificationSettingsRepository, MongoNotificationSettingsRepository>();

        // Servicio de migración de SQL a MongoDB Atlas
        services.AddScoped<SqlToMongoMigrationService>();

        return services;
    }
}
