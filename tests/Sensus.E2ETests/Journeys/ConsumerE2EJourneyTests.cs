using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sensus.Application.Media.DTOs;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Application.Users.Commands.Login;
using Sensus.Application.Users.Commands.RegisterUser;
using Sensus.Application.Users.Commands.RequestPasswordReset;
using Sensus.Application.Users.Commands.ResetPassword;
using Sensus.Application.Users.Commands.UpdateProfile;
using Sensus.Application.Users.DTOs;
using Sensus.E2ETests.Infrastructure;

namespace Sensus.E2ETests.Journeys;

public class ConsumerE2EJourneyTests : IClassFixture<SensusE2EWebApplicationFactory>
{
    private readonly SensusE2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConsumerE2EJourneyTests(SensusE2EWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Consumer_FullLifecycle_Register_Profile_Recover_Delete_SucceedsEndToEnd()
    {
        // 1. SE-001: Registro de usuario nuevo
        var email = $"e2e.consumer.{Guid.NewGuid():N}@sensus.com";
        var passwordOriginal = "Password123#";
        var registerCommand = new RegisterUserCommand("Consumidor E2E", email, passwordOriginal, true);

        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerCommand);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var authData = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authData.Should().NotBeNull();
        authData!.Token.Should().NotBeNullOrWhiteSpace();
        var token = authData.Token;

        // 2. SE-006: Consultar perfil inicial
        using var profileReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/profile");
        profileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var profileRes = await _client.SendAsync(profileReq);
        profileRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var profileDto = await profileRes.Content.ReadFromJsonAsync<UserDto>();
        profileDto!.NombreCompleto.Should().Be("Consumidor E2E");

        // 3. Spec 003/022: Firma heredada retirada (410 Gone); foto se sube por POST /api/v1/media/upload moderado
        using var sigReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/photo-signature");
        sigReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sigRes = await _client.SendAsync(sigReq);
        sigRes.StatusCode.Should().Be(HttpStatusCode.Gone);

        using var uploadForm = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }; // Firma JPEG JFIF válida
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        uploadForm.Add(fileContent, "file", "avatar.jpg");
        using var uploadReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/upload")
        {
            Content = uploadForm
        };
        uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadRes = await _client.SendAsync(uploadReq);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var mediaDto = await uploadRes.Content.ReadFromJsonAsync<UploadMediaResponseDto>();
        mediaDto.Should().NotBeNull();
        var photoUrl = mediaDto!.SecureUrl!;

        // 4. SE-006: Actualizar perfil con foto aprobada del usuario
        using var updateReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/profile")
        {
            Content = JsonContent.Create(new UpdateProfileCommand("Consumidor Actualizado", photoUrl))
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedProfile = await updateRes.Content.ReadFromJsonAsync<UserDto>();
        updatedProfile!.NombreCompleto.Should().Be("Consumidor Actualizado");
        updatedProfile.FotoPerfilUrl.Should().Be(photoUrl);

        // 5. SE-005: Recuperación de contraseña por correo
        _factory.FakeEmailSender.SentEmails.Clear();
        var recoverRes = await _client.PostAsJsonAsync("/api/v1/auth/recover", new RequestPasswordResetCommand(email));
        recoverRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _factory.FakeEmailSender.SentEmails.Should().ContainSingle();
        var sentHtml = _factory.FakeEmailSender.SentEmails[0].Html;
        var match = Regex.Match(sentHtml, @"token=([a-f0-9]{64})");
        match.Success.Should().BeTrue();
        var rawResetToken = match.Groups[1].Value;

        // 6. SE-005: Restablecer contraseña con el token temporal
        var passwordNuevo = "NewPassword456#";
        var resetRes = await _client.PostAsJsonAsync("/api/v1/auth/recover/confirm", new ResetPasswordCommand(rawResetToken, passwordNuevo));
        resetRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. SE-002: Iniciar sesión con la nueva contraseña
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, passwordNuevo));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuthData = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        var newToken = newAuthData!.Token;

        // 8. SE-009: Eliminar cuenta con confirmación de contraseña
        using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/account")
        {
            Content = JsonContent.Create(new DeleteAccountCommand(passwordNuevo))
        };
        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        var deleteRes = await _client.SendAsync(deleteReq);
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 9. Verificar que el usuario ya no existe ni puede iniciar sesión
        var reloginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, passwordNuevo));
        reloginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
