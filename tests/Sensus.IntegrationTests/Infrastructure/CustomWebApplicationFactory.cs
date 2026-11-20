using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Services;
using Sensus.TestKit.Fakes;

namespace Sensus.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public IUserRepository UserRepository { get; } = Substitute.For<IUserRepository>();
    public IComercioRepository ComercioRepository { get; } = Substitute.For<IComercioRepository>();
    public IOfferRepository OfferRepository { get; } = Substitute.For<IOfferRepository>();
    public IOfferClaimRepository OfferClaimRepository { get; } = Substitute.For<IOfferClaimRepository>();
    public IOfferViewRepository OfferViewRepository { get; } = Substitute.For<IOfferViewRepository>();
    public IPasswordResetTokenRepository PasswordResetTokenRepository { get; } = Substitute.For<IPasswordResetTokenRepository>();
    public FakeProfileImageStorage FakeImageStorage { get; } = new();
    public FakeDateTimeProvider FakeClock { get; } = new();
    public FakeGeocodingService FakeGeocoding { get; } = new();
    public FakeEmailSender FakeEmailSender { get; } = new();
    public IMediaAssetRepository MediaAssetRepository { get; } = Substitute.For<IMediaAssetRepository>();
    public RecordingMetricsPublisher Metrics { get; } = new();
    public IAiConversationRepository AiConversationRepository { get; } = Substitute.For<IAiConversationRepository>();
    public IAiMessageRepository AiMessageRepository { get; } = Substitute.For<IAiMessageRepository>();
    public IAiTraceLogRepository AiTraceLogRepository { get; } = Substitute.For<IAiTraceLogRepository>();
    public IAiChatGateway FakeAiChatGateway { get; } = Substitute.For<IAiChatGateway>();
    public ITextModerationService TextModerationService { get; } = Substitute.For<ITextModerationService>();
    public ICacheService CacheService { get; } = Substitute.For<ICacheService>();
    public IBudgetCounterStore BudgetCounterStore { get; } = Substitute.For<IBudgetCounterStore>();
    public ISanctionRepository SanctionRepository { get; } = Substitute.For<ISanctionRepository>();
    public IDeviceRepository DeviceRepository { get; } = Substitute.For<IDeviceRepository>();
    public IOfferLikeRepository OfferLikeRepository { get; } = Substitute.For<IOfferLikeRepository>();
    public IStoreReviewRepository StoreReviewRepository { get; } = Substitute.For<IStoreReviewRepository>();
    public IFriendRequestRepository FriendRequestRepository { get; } = Substitute.For<IFriendRequestRepository>();
    public ISharedOfferRepository SharedOfferRepository { get; } = Substitute.For<ISharedOfferRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Las pruebas nunca se conectan a un MongoDB real: se ignora cualquier cadena de conexión
            // que llegue por user-secrets o variables de entorno del equipo (AGENTS.md §11, FR-016-02).
            services.PostConfigure<MongoDbSettings>(settings => settings.ConnectionString = string.Empty);

            // Reemplazar servicios con fakes y mocks controlados para pruebas de integración
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(UserRepository);

            services.RemoveAll<IComercioRepository>();
            services.AddSingleton(ComercioRepository);

            services.RemoveAll<IDeviceRepository>();
            services.AddSingleton(Substitute.For<IDeviceRepository>());

            services.RemoveAll<INotificationSettingsRepository>();
            services.AddSingleton(Substitute.For<INotificationSettingsRepository>());

            services.RemoveAll<INotificationLocationRepository>();
            services.AddSingleton(Substitute.For<INotificationLocationRepository>());

            services.RemoveAll<IMediaAssetRepository>();
            services.AddSingleton(MediaAssetRepository);

            services.RemoveAll<IRequestMetricsPublisher>();
            services.AddSingleton<IRequestMetricsPublisher>(Metrics);

            services.RemoveAll<IOfferRepository>();
            services.AddSingleton(OfferRepository);

            services.RemoveAll<IOfferClaimRepository>();
            services.AddSingleton(OfferClaimRepository);

            services.RemoveAll<IOfferViewRepository>();
            services.AddSingleton(OfferViewRepository);

            services.Configure<ClaimCodeSettings>(settings =>
                settings.Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

            services.RemoveAll<IPasswordResetTokenRepository>();
            services.AddSingleton(PasswordResetTokenRepository);

            services.RemoveAll<IProfileImageStorage>();
            services.AddSingleton<IProfileImageStorage>(FakeImageStorage);

            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(FakeClock);

            services.RemoveAll<IGeocodingService>();
            services.AddSingleton<IGeocodingService>(FakeGeocoding);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(FakeEmailSender);

            // Sanciones (Spec 020): sin MongoDB real en los tests de endpoints; sin dispositivos que notificar
            services.RemoveAll<ISanctionRepository>();
            services.AddSingleton(SanctionRepository);

            DeviceRepository.GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            services.RemoveAll<IDeviceRepository>();
            services.AddSingleton(DeviceRepository);

            // Sensi IA (Spec 046)
            services.RemoveAll<IAiConversationRepository>();
            services.AddSingleton(AiConversationRepository);

            services.RemoveAll<IAiMessageRepository>();
            services.AddSingleton(AiMessageRepository);

            services.RemoveAll<IAiTraceLogRepository>();
            services.AddSingleton(AiTraceLogRepository);

            services.RemoveAll<IAiChatGateway>();
            services.AddSingleton(FakeAiChatGateway);

            TextModerationService.ModerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new TextModerationResult(true, Decision: "Approved"));
            TextModerationService.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new TextModerationResult(true, Decision: "Approved"));
            services.RemoveAll<ITextModerationService>();
            services.AddSingleton(TextModerationService);

            CacheService.IsAvailable.Returns(true);
            CacheService.GetAsync<long?>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((long?)0);
            CacheService.GetAsync<double?>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((double?)0.0);
            services.RemoveAll<ICacheService>();
            services.AddSingleton(CacheService);

            // Contadores de presupuesto de Sensi IA (Spec 049): disponibles y sin consumo previo
            BudgetCounterStore.IsAvailable.Returns(true);
            BudgetCounterStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((double?)0d);
            BudgetCounterStore.IncrementAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns((double?)1d);
            services.RemoveAll<IBudgetCounterStore>();
            services.AddSingleton(BudgetCounterStore);

            OfferLikeRepository.CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<Guid, int>());
            OfferLikeRepository.GetLikedOfferIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
                .Returns([]);
            OfferLikeRepository.GetByUserIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns([]);
            services.RemoveAll<IOfferLikeRepository>();
            services.AddSingleton(OfferLikeRepository);

            StoreReviewRepository.GetByMerchantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);
            StoreReviewRepository.GetRatingSummariesByMerchantIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<Guid, (double AverageRating, int ReviewCount)>());
            StoreReviewRepository.GetRecentActivityByUserIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns([]);
            services.RemoveAll<IStoreReviewRepository>();
            services.AddSingleton(StoreReviewRepository);

            FriendRequestRepository.GetForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);
            services.RemoveAll<IFriendRequestRepository>();
            services.AddSingleton(FriendRequestRepository);

            services.RemoveAll<ISharedOfferRepository>();
            services.AddSingleton(SharedOfferRepository);
        });
    }
}
