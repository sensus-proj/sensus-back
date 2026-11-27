using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.TestKit.Fakes;

namespace Sensus.E2ETests.Infrastructure;

public class SensusE2EWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemoryUserRepository UserRepository { get; } = new();
    public InMemoryComercioRepository ComercioRepository { get; } = new();
    public InMemoryOfferRepository OfferRepository { get; } = new();
    public InMemoryPasswordResetTokenRepository PasswordResetTokenRepository { get; } = new();
    public InMemoryRefreshTokenRepository RefreshTokenRepository { get; } = new();
    public FakeProfileImageStorage FakeImageStorage { get; } = new();
    public FakeDateTimeProvider FakeClock { get; } = new();
    public FakeGeocodingService FakeGeocoding { get; } = new();
    public FakeEmailSender FakeEmailSender { get; } = new();
    public InMemoryAiConversationRepository AiConversationRepository { get; } = new();
    public InMemoryAiMessageRepository AiMessageRepository { get; } = new();
    public InMemoryAiTraceLogRepository AiTraceLogRepository { get; } = new();
    public InMemoryFriendRequestRepository FriendRequestRepository { get; } = new();
    public InMemorySharedOfferRepository SharedOfferRepository { get; } = new();
    public InMemoryUserInterestsRepository UserInterestsRepository { get; } = new();
    public InMemoryOfferInteractionRepository OfferInteractionRepository { get; } = new();
    public InMemoryOfferLikeRepository OfferLikeRepository { get; } = new();
    public InMemoryStoreReviewRepository StoreReviewRepository { get; } = new();
    public InMemoryFeedCommentRepository FeedCommentRepository { get; } = new();
    public InMemoryFeedPostRepository FeedPostRepository { get; } = new();
    public InMemoryOfferClaimRepository OfferClaimRepository { get; } = new();
    public InMemorySanctionRepository SanctionRepository { get; } = new();
    public InMemoryModerationAuditRepository ModerationAuditRepository { get; } = new();
    public InMemoryFeedPostVisibilityLookup FeedPostVisibilityLookup { get; } = new();
    public InMemoryContentReportRepository ContentReportRepository { get; } = new();
    public InMemoryReportTargetService ReportTargetService { get; } = new();
    public InMemoryOfferStatusHistoryRepository OfferStatusHistoryRepository { get; } = new();
    public InMemoryMediaAssetRepository MediaAssetRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<MongoDbSettings>(settings => settings.ConnectionString = string.Empty);

            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(UserRepository);
            services.RemoveAll<INotificationLocationRepository>();
            services.AddSingleton<INotificationLocationRepository, InMemoryNotificationLocationRepository>();

            services.RemoveAll<IComercioRepository>();
            services.AddSingleton<IComercioRepository>(ComercioRepository);

            services.RemoveAll<IOfferRepository>();
            services.AddSingleton<IOfferRepository>(OfferRepository);

            services.RemoveAll<IPasswordResetTokenRepository>();
            services.AddSingleton<IPasswordResetTokenRepository>(PasswordResetTokenRepository);

            services.RemoveAll<IRefreshTokenRepository>();
            services.AddSingleton<IRefreshTokenRepository>(RefreshTokenRepository);

            services.RemoveAll<IProfileImageStorage>();
            services.AddSingleton<IProfileImageStorage>(FakeImageStorage);

            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(FakeClock);

            services.RemoveAll<IGeocodingService>();
            services.AddSingleton<IGeocodingService>(FakeGeocoding);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(FakeEmailSender);

            // Sensi IA (Spec 046) - Repositorios en memoria para pruebas E2E
            services.RemoveAll<IAiConversationRepository>();
            services.AddSingleton<IAiConversationRepository>(AiConversationRepository);

            services.RemoveAll<IAiMessageRepository>();
            services.AddSingleton<IAiMessageRepository>(AiMessageRepository);

            services.RemoveAll<IAiTraceLogRepository>();
            services.AddSingleton<IAiTraceLogRepository>(AiTraceLogRepository);

            services.RemoveAll<IFriendRequestRepository>();
            services.AddSingleton<IFriendRequestRepository>(FriendRequestRepository);

            services.RemoveAll<ISharedOfferRepository>();
            services.AddSingleton<ISharedOfferRepository>(SharedOfferRepository);

            services.RemoveAll<IUserInterestsRepository>();
            services.AddSingleton<IUserInterestsRepository>(UserInterestsRepository);

            services.RemoveAll<IOfferInteractionRepository>();
            services.AddSingleton<IOfferInteractionRepository>(OfferInteractionRepository);
            services.Configure<InteractionSettings>(settings =>
            {
                settings.ActiveKeyVersion = 1;
                settings.HmacKeys[1] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            });

            services.Configure<MfaSettings>(settings =>
            {
                settings.EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                settings.Issuer = "Sensus";
                settings.StepUpWindowMinutes = 15;
                settings.ChallengeExpiryMinutes = 5;
                settings.MaxFailedAttempts = 5;
                settings.LockoutMinutes = 15;
            });
            services.RemoveAll<IOfferLikeRepository>();
            services.AddSingleton<IOfferLikeRepository>(OfferLikeRepository);

            services.RemoveAll<IStoreReviewRepository>();
            services.AddSingleton<IStoreReviewRepository>(StoreReviewRepository);

            services.RemoveAll<IFeedCommentRepository>();
            services.AddSingleton<IFeedCommentRepository>(FeedCommentRepository);

            // Cierre de contenido al dar de baja la cuenta (Spec 061): sin estos sustitutos el handler
            // resolvería repositorios MongoDB y la baja E2E terminaría en 500 sin conexión configurada.
            services.RemoveAll<IFeedPostRepository>();
            services.AddSingleton<IFeedPostRepository>(FeedPostRepository);

            services.RemoveAll<IOfferClaimRepository>();
            services.AddSingleton<IOfferClaimRepository>(OfferClaimRepository);

            services.RemoveAll<ISanctionRepository>();
            services.AddSingleton<ISanctionRepository>(SanctionRepository);

            services.RemoveAll<IModerationAuditRepository>();
            services.AddSingleton<IModerationAuditRepository>(ModerationAuditRepository);

            services.RemoveAll<IFeedPostVisibilityLookup>();
            services.AddSingleton<IFeedPostVisibilityLookup>(FeedPostVisibilityLookup);

            services.RemoveAll<IContentReportRepository>();
            services.AddSingleton<IContentReportRepository>(ContentReportRepository);

            services.RemoveAll<IReportTargetService>();
            services.AddSingleton<IReportTargetService>(ReportTargetService);

            services.RemoveAll<IOfferStatusHistoryRepository>();
            services.AddSingleton<IOfferStatusHistoryRepository>(OfferStatusHistoryRepository);

            services.Configure<CloudinaryAccountSettings>(settings =>
                settings.CloudName = "sensus");

            services.RemoveAll<IMediaAssetRepository>();
            services.AddSingleton<IMediaAssetRepository>(MediaAssetRepository);

            services.RemoveAll<IImageModerationService>();
            services.AddSingleton<IImageModerationService>(new FakeApprovingImageModerationService());

            services.RemoveAll<IMediaStorage>();
            services.AddSingleton<IMediaStorage>(new FakeE2EMediaStorage());

            services.RemoveAll<IModerationEvidenceStore>();
            services.AddSingleton<IModerationEvidenceStore>(new FakeE2EModerationEvidenceStore());

            services.RemoveAll<ICdnCacheInvalidator>();
            services.AddSingleton<ICdnCacheInvalidator>(new FakeE2ECdnCacheInvalidator());

            services.RemoveAll<ITextModerationService>();
            services.AddSingleton<ITextModerationService>(new FakeApprovingTextModerationService());
        });
    }

    private sealed class FakeApprovingTextModerationService : ITextModerationService
    {
        public Task<TextModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TextModerationResult(true, "Texto aprobado.", [], "Approved"));

        public Task<TextModerationResult> ModerateContentTextAsync(
            string text,
            string contentContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TextModerationResult(true, "Texto aprobado.", [], "Approved"));
    }

    private sealed class FakeApprovingImageModerationService : IImageModerationService
    {
        public Task<ImageModerationResult> ModerateImageBytesAsync(
            byte[] imageBytes,
            string contentType,
            string? textContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImageModerationResult(true, "Imagen aprobada.", [], "Approved"));
    }

    private sealed class FakeE2EMediaStorage : IMediaStorage
    {
        public Task<Result<MediaUploadResult>> UploadImageAsync(
            byte[] content,
            string contentType,
            Guid ownerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new MediaUploadResult(
                $"sensus/media/{ownerId:D}/e2e-avatar",
                $"https://res.cloudinary.com/sensus/image/authenticated/v1/sensus/media/{ownerId:D}/e2e-avatar.jpg")));

        public Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeE2EModerationEvidenceStore : IModerationEvidenceStore
    {
        public Task AppendAsync(ModerationEvidence evidence, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeE2ECdnCacheInvalidator : ICdnCacheInvalidator
    {
        public Task<bool> InvalidateAsync(string publicId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
