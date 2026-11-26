using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Endpoints;

public sealed class MediaEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly byte[] ValidJpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandaloneModerationRoute_IsUnavailable(bool authenticated)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/moderate");
        if (authenticated)
        {
            var tokenGenerator = factory.Services.GetRequiredService<IJwtTokenGenerator>();
            var (token, _) = tokenGenerator.GenerateGuestToken();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadMedia_RequiresAuthentication()
    {
        using var client = factory.CreateClient();
        using var form = BuildMultipart(ValidJpegBytes, "image/jpeg", "foto.jpg");

        using var response = await client.PostAsync("/api/v1/media/upload", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadMedia_ModeratesBeforeUploading_AndReturns201WhenApproved()
    {
        var imageMod = Substitute.For<IImageModerationService>();
        imageMod.ModerateImageBytesAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true, "Aprobado", [], "Approved"));
        var mediaStorage = Substitute.For<IMediaStorage>();
        mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Sensus.Domain.Common.Result.Success(new MediaUploadResult(
                "sensus/media/u/ok",
                "https://res.cloudinary.com/demo/image/authenticated/v1/sensus/media/u/ok.jpg")));

        using var custom = factory.WithWebHostBuilder(b => b.ConfigureServices((_, services) =>
        {
            services.Replace(ServiceDescriptor.Singleton(imageMod));
            services.Replace(ServiceDescriptor.Singleton(mediaStorage));
        }));
        using var client = custom.CreateClient();
        string token = CreateUserToken(custom);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var form = BuildMultipart(ValidJpegBytes, "image/jpeg", "foto.jpg");
        using var response = await client.PostAsync("/api/v1/media/upload", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UploadMediaResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal("Approved", dto.Status);
        Received.InOrder(() =>
        {
            imageMod.ModerateImageBytesAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<string?>(), Arg.Any<CancellationToken>());
            mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task UploadMedia_DoesNotCallStorage_AndReturns202WhenQuarantined()
    {
        var imageMod = Substitute.For<IImageModerationService>();
        imageMod.ModerateImageBytesAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "Proveedor no disponible", null, "Quarantine"));
        var mediaStorage = Substitute.For<IMediaStorage>();

        using var custom = factory.WithWebHostBuilder(b => b.ConfigureServices((_, services) =>
        {
            services.Replace(ServiceDescriptor.Singleton(imageMod));
            services.Replace(ServiceDescriptor.Singleton(mediaStorage));
        }));
        using var client = custom.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(custom));

        using var form = BuildMultipart(ValidJpegBytes, "image/jpeg", "foto.jpg");
        using var response = await client.PostAsync("/api/v1/media/upload", form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UploadMediaResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal("Quarantined", dto.Status);
        Assert.Null(dto.SecureUrl);
        await mediaStorage.DidNotReceiveWithAnyArgs().UploadImageAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task UploadMedia_RejectsSpoofedExecutableOrOversizedPayload()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserToken(factory));

        using var spoofedForm = BuildMultipart([0x4D, 0x5A, 0x90, 0x00], "image/jpeg", "malware.jpg");
        using var spoofedResp = await client.PostAsync("/api/v1/media/upload", spoofedForm);
        Assert.Equal(HttpStatusCode.BadRequest, spoofedResp.StatusCode);

        using var oversizedForm = BuildMultipart(new byte[4_500_001], "image/jpeg", "grande.jpg");
        using var oversizedResp = await client.PostAsync("/api/v1/media/upload", oversizedForm);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedResp.StatusCode);
    }

    private string CreateUserToken(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> hostFactory)
    {
        var user = User.Create("Usuario Media", $"media.{Guid.NewGuid():N}@example.test", "hash", true, DateTime.UtcNow).Value;
        factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGen = hostFactory.Services.GetRequiredService<IJwtTokenGenerator>();
        return tokenGen.GenerateToken(user).Token;
    }

    private static MultipartFormDataContent BuildMultipart(byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }
}
