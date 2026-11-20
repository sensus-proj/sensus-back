using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
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
        });
    }
}
