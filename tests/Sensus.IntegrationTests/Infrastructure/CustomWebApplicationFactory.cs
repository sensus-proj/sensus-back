using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Repositories;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Reemplazar servicios con fakes y mocks controlados para pruebas de integración
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(UserRepository);

            services.RemoveAll<IComercioRepository>();
            services.AddSingleton(ComercioRepository);

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
