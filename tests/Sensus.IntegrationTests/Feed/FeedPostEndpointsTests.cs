using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Feed;

public sealed class FeedPostEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FeedPostEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.FeedPostRepository.ClearReceivedCalls();
        _factory.MediaStorage.ClearReceivedCalls();
        _factory.ImageModerationService.ClearReceivedCalls();

        // En este fixture la imagen se aprueba para poder verificar la subida y persistencia (Spec 024).
        _factory.ImageModerationService.ModerateImageBytesAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true, "Imagen aprobada."));
    }

    private (string Token, User User) CreateRegisteredToken(string? role = null)
    {
        User user = User.Create(
            "Carlos Pérez",
            $"carlos.{Guid.NewGuid():N}@sensus.com",
            "hash",
            true,
            DateTime.UtcNow).Value;

        if (!string.IsNullOrWhiteSpace(role))
        {
            user.AddRole(role);
        }

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        return (tokenGenerator.GenerateToken(user).Token, user);
    }

    [Fact]
    public async Task CreatePost_ReturnsCreated_WhenValid()
    {
        (string token, User user) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Probando la nueva función del feed social" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FeedPostDto? post = await response.Content.ReadFromJsonAsync<FeedPostDto>();
        post.Should().NotBeNull();
        post!.Content.Should().Be("Probando la nueva función del feed social");
        post.AuthorUserId.Should().Be(user.Id);
        post.AuthorDisplayName.Should().Be("Carlos Pérez");
        post.Status.Should().Be(FeedPostStatus.Published.ToString());
        response.Headers.Location.Should().NotBeNull();

        await _factory.FeedPostRepository.Received(1).AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenContentIsInvalid()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Contenido vacío
        HttpResponseMessage emptyResp = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "   " });
        emptyResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Contenido que supera 500 caracteres
        string overLimit = new('a', 501);
        HttpResponseMessage overLimitResp = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = overLimit });
        overLimitResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePost_ReturnsUnauthorized_WhenNoTokenProvided()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Contenido sin token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_ReturnsAcceptedAndQuarantinesWithoutUpload_WhenImageModerationQuarantines()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _factory.ImageModerationService.ModerateImageBytesAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "Proveedor no disponible", null, "Quarantine"));

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Post con imagen en espera"), "content");
        using var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "image", "foto.jpg");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/feed/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        FeedPostDto? dto = await response.Content.ReadFromJsonAsync<FeedPostDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(FeedPostStatus.Quarantined.ToString());
        dto.ImageUrl.Should().BeNull();
        await _factory.MediaStorage.DidNotReceive().UploadImageAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _factory.FeedPostRepository.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.Status == FeedPostStatus.Quarantined && p.ImageUrl == null),
            Arg.Any<CancellationToken>());
        await _factory.ModerationAuditRepository.Received().AddAsync(
            Arg.Is<ModerationAuditRecord>(r =>
                r.TargetType == ModerationTargetType.FeedPost
                && r.Verdict == ModerationVerdict.Quarantined),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequestWithoutUpload_WhenImageModerationRejects()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _factory.ImageModerationService.ModerateImageBytesAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "Contenido prohibido", ["violence"], "Rejected"));

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Post con imagen prohibida"), "content");
        using var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "image", "foto.jpg");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/feed/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.MediaStorage.DidNotReceive().UploadImageAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _factory.FeedPostRepository.DidNotReceive().AddAsync(
            Arg.Any<FeedPost>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsForbidden_WhenGuestTokenUsed()
    {
        string guestToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Intento de invitado" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletePost_ReturnsNoContent_WhenAuthorDeletes()
    {
        (string token, User author) = CreateRegisteredToken();
        Guid postId = Guid.NewGuid();
        FeedPost post = FeedPost.Create(author.Id, author.NombreCompleto, "Mi post a borrar", null, DateTime.UtcNow, id: postId).Value;
        _factory.FeedPostRepository.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/feed/posts/{postId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        post.IsDeleted.Should().BeTrue();
        await _factory.FeedPostRepository.Received(1).UpdateAsync(post, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePost_ReturnsNoContent_WhenAdminDeletes()
    {
        (string adminToken, User admin) = CreateRegisteredToken(role: "Administrador");
        Guid authorId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        FeedPost post = FeedPost.Create(authorId, "Otro Autor", "Post moderado por admin", null, DateTime.UtcNow, id: postId).Value;
        _factory.FeedPostRepository.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/feed/posts/{postId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        post.IsDeleted.Should().BeTrue();
        await _factory.FeedPostRepository.Received(1).UpdateAsync(post, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePost_ReturnsForbidden_WhenAnotherUserAttemptsDeletion()
    {
        (string token, _) = CreateRegisteredToken();
        Guid otherAuthorId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        FeedPost post = FeedPost.Create(otherAuthorId, "Otro", "Post de otro", null, DateTime.UtcNow, id: postId).Value;
        _factory.FeedPostRepository.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/feed/posts/{postId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        post.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_ReturnsNotFound_WhenPostDoesNotExist()
    {
        (string token, _) = CreateRegisteredToken();
        Guid missingId = Guid.NewGuid();
        _factory.FeedPostRepository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((FeedPost?)null);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/feed/posts/{missingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePost_Multipart_ModeratesAndStoresImageBeforePublishing()
    {
        (string token, User user) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Publicación con imagen moderada"), "content");
        var imageContent = new ByteArrayContent([1, 2, 3]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(imageContent, "image", "foto.jpg");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/feed/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FeedPostDto? post = await response.Content.ReadFromJsonAsync<FeedPostDto>();
        post!.ImageUrl.Should().Be("https://res.cloudinary.com/sensus/image/upload/v1/posts/sample.jpg");
        await _factory.ImageModerationService.Received(1).ModerateImageBytesAsync(
            Arg.Any<byte[]>(),
            "image/jpeg",
            "Publicación con imagen moderada",
            Arg.Any<CancellationToken>());
        await _factory.MediaStorage.Received(1).UploadImageAsync(
            Arg.Any<byte[]>(),
            "image/jpeg",
            user.Id,
            Arg.Any<CancellationToken>());
        await _factory.FeedPostRepository.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => !string.IsNullOrWhiteSpace(p.ImagePublicId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_Multipart_RejectsUnsupportedImageType()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Publicación con imagen inválida"), "content");
        var imageContent = new ByteArrayContent([1, 2, 3]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(imageContent, "image", "documento.pdf");

        HttpResponseMessage response = await _client.PostAsync("/api/v1/feed/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ImageModerationService.DidNotReceive().ModerateImageBytesAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _factory.MediaStorage.DidNotReceive().UploadImageAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _factory.FeedPostRepository.DidNotReceive().AddAsync(
            Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePost_PurgesStoredImage()
    {
        (string token, User author) = CreateRegisteredToken();
        Guid postId = Guid.NewGuid();
        FeedPost post = FeedPost.Create(
            author.Id,
            author.NombreCompleto,
            "Post con imagen",
            "https://res.cloudinary.com/sensus/image/upload/v1/posts/sample.jpg",
            DateTime.UtcNow,
            id: postId,
            imagePublicId: "posts/sample").Value;
        _factory.FeedPostRepository.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/feed/posts/{postId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.MediaStorage.Received(1).DeleteImageAsync("posts/sample", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenExternalImageUrlSentInJson()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Intento de inyectar URL externa", imageUrl = "https://evil.example.com/unmoderated.jpg" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FeedPostRepository.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsCreated_WhenApprovedOwnedMediaAssetIdSent()
    {
        (string token, User user) = CreateRegisteredToken();
        MediaAsset asset = MediaAsset.Create(
            user.Id,
            "posts/owned-approved",
            "https://res.cloudinary.com/sensus/image/upload/v1/owned-approved.jpg",
            "image/jpeg",
            MediaAssetStatus.Approved,
            DateTime.UtcNow);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Publicación con activo aprobado", mediaAssetId = asset.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FeedPostDto? post = await response.Content.ReadFromJsonAsync<FeedPostDto>();
        post!.ImageUrl.Should().Be(asset.SecureUrl);
        await _factory.FeedPostRepository.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.ImagePublicId == "posts/owned-approved" && p.ImageUrl == asset.SecureUrl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenMediaAssetIsForeignOrNotApproved()
    {
        (string token, User user) = CreateRegisteredToken();
        MediaAsset foreignAsset = MediaAsset.Create(
            Guid.NewGuid(),
            "posts/foreign",
            "https://res.cloudinary.com/sensus/image/upload/v1/foreign.jpg",
            "image/jpeg",
            MediaAssetStatus.Approved,
            DateTime.UtcNow);
        MediaAsset quarantinedAsset = MediaAsset.Create(
            user.Id,
            "posts/quarantined",
            "https://res.cloudinary.com/sensus/image/upload/v1/quarantined.jpg",
            "image/jpeg",
            MediaAssetStatus.Quarantined,
            DateTime.UtcNow);
        _factory.MediaAssetRepository.GetByIdAsync(foreignAsset.Id, Arg.Any<CancellationToken>()).Returns(foreignAsset);
        _factory.MediaAssetRepository.GetByIdAsync(quarantinedAsset.Id, Arg.Any<CancellationToken>()).Returns(quarantinedAsset);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage foreignResp = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Intento con asset ajeno", mediaAssetId = foreignAsset.Id });
        foreignResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage quarantinedResp = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Intento con asset en cuarentena", mediaAssetId = quarantinedAsset.Id });
        quarantinedResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.FeedPostRepository.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenRetiredMediaIdAliasSentWithoutMediaAssetId()
    {
        (string token, User user) = CreateRegisteredToken();
        MediaAsset asset = MediaAsset.Create(
            user.Id,
            "posts/owned-approved",
            "https://res.cloudinary.com/sensus/image/upload/v1/owned-approved.jpg",
            "image/jpeg",
            MediaAssetStatus.Approved,
            DateTime.UtcNow);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Alias heredado", mediaId = asset.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Feed.MediaIdRetired");
        await _factory.FeedPostRepository.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }
}
