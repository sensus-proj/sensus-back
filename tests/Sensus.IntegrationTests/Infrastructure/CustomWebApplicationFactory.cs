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
        });
    }
}
