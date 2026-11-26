using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Users.Commands.EnrollMfa;
using Sensus.Application.Users.Commands.VerifyMfa;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

public sealed class MfaHandlersTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IModerationAuditRecorder _auditRecorder = Substitute.For<IModerationAuditRecorder>();
    private readonly DateTime _now = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

    public MfaHandlersTests()
    {
        _clock.UtcNow.Returns(_now);
        _auditRecorder.RecordBestEffortAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private static User CreateAdminUser()
    {
        var user = User.Create("Admin Sensus", "admin@sensus.app", "hash", true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Value;
        user.AddRole(RoleType.Administrador);
        return user;
    }

    [Fact]
    public async Task EnrollMfa_ForNonAdminUser_ReturnsForbidden()
    {
        var consumer = User.Create("Consumidor", "c@sensus.app", "hash", true, _now).Value;
        _currentUser.UserId.Returns(consumer.Id.ToString());
        _userRepository.GetByIdAsync(consumer.Id, Arg.Any<CancellationToken>()).Returns(consumer);

        var handler = new EnrollMfaCommandHandler(_currentUser, _userRepository, _totpService, _auditRecorder);
        var result = await handler.Handle(new EnrollMfaCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task EnrollMfa_ForAdmin_EncryptsSecretStoresHashedRecoveryCodesAndReturnsUri()
    {
        var admin = CreateAdminUser();
        _currentUser.UserId.Returns(admin.Id.ToString());
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _totpService.GenerateSecret(Arg.Any<int>()).Returns("JBSWY3DPEHPK3PXP");
        _totpService.EncryptSecret("JBSWY3DPEHPK3PXP").Returns(Result.Success("enc:v1:abc"));
        _totpService.BuildOtpAuthUri("Sensus", admin.Correo, "JBSWY3DPEHPK3PXP")
            .Returns("otpauth://totp/Sensus:admin%40sensus.app?secret=JBSWY3DPEHPK3PXP&issuer=Sensus");
        _totpService.GenerateRecoveryCodes(8).Returns(["A1-B1", "A2-B2", "A3-B3", "A4-B4", "A5-B5", "A6-B6", "A7-B7", "A8-B8"]);
        _totpService.HashRecoveryCode(Arg.Any<string>()).Returns(ci => $"hash:{ci.ArgAt<string>(0)}");

        var handler = new EnrollMfaCommandHandler(_currentUser, _userRepository, _totpService, _auditRecorder);
        var result = await handler.Handle(new EnrollMfaCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be("JBSWY3DPEHPK3PXP");
        result.Value.RecoveryCodes.Should().HaveCount(8);
        admin.Mfa.Should().NotBeNull();
        admin.Mfa!.IsPendingConfirmation.Should().BeTrue();
        admin.Mfa.IsEnabled.Should().BeFalse();
        admin.Mfa.EncryptedSecret.Should().Be("enc:v1:abc");
        admin.Mfa.RecoveryCodeHashes.Should().HaveCount(8).And.OnlyContain(h => h.StartsWith("hash:"));
        await _userRepository.Received(1).UpdateAsync(admin, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyMfa_WithValidTotp_ActivatesMfaIssuesStepUpJwtAndAudits()
    {
        var admin = CreateAdminUser();
        admin.BeginMfaEnrollment("enc:secret", ["hash:c1", "hash:c2"]);
        _currentUser.UserId.Returns(admin.Id.ToString());
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _totpService.DecryptSecret("enc:secret").Returns(Result.Success("PLAINSECRET"));
        _totpService.TryVerifyCode("PLAINSECRET", "123456", _now, out Arg.Any<long>(), 1)
            .Returns(ci =>
            {
                ci[3] = 58000L;
                return true;
            });
        _jwtTokenGenerator.GenerateAdminStepUpToken(admin, _now).Returns(("stepup.jwt", _now.AddMinutes(15)));

        var handler = new VerifyMfaCommandHandler(_currentUser, _userRepository, _totpService, _jwtTokenGenerator, _clock, _auditRecorder);
        var result = await handler.Handle(new VerifyMfaCommand(Code: "123456"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("stepup.jwt");
        admin.Mfa!.IsEnabled.Should().BeTrue();
        admin.Mfa.IsPendingConfirmation.Should().BeFalse();
        admin.Mfa.LastVerifiedTimeStep.Should().Be(58000L);
        await _auditRecorder.Received(1).RecordBestEffortAsync(
            Arg.Is<ModerationAuditEntry>(e => e.TargetType == ModerationTargetType.AdminMfa && e.TargetId == admin.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyMfa_AfterFiveInvalidAttempts_LocksAccountTemporarily()
    {
        var admin = CreateAdminUser();
        admin.BeginMfaEnrollment("enc:secret", ["hash:c1"]);
        admin.CompleteMfaVerification(100L, _now.AddMinutes(-10));
        _currentUser.UserId.Returns(admin.Id.ToString());
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _totpService.DecryptSecret("enc:secret").Returns(Result.Success("PLAINSECRET"));
        _totpService.TryVerifyCode("PLAINSECRET", "000000", _now, out Arg.Any<long>(), 1).Returns(false);

        var handler = new VerifyMfaCommandHandler(
            _currentUser,
            _userRepository,
            _totpService,
            _jwtTokenGenerator,
            _clock,
            _auditRecorder,
            mfaSettings: Options.Create(new MfaSettings { MaxFailedAttempts = 5, LockoutMinutes = 15 }));

        for (int i = 0; i < 4; i++)
        {
            var attempt = await handler.Handle(new VerifyMfaCommand(Code: "000000"), CancellationToken.None);
            attempt.IsFailure.Should().BeTrue();
            attempt.Error.Type.Should().Be(ErrorType.Unauthorized);
        }

        var fifthAttempt = await handler.Handle(new VerifyMfaCommand(Code: "000000"), CancellationToken.None);
        fifthAttempt.IsFailure.Should().BeTrue();
        fifthAttempt.Error.Type.Should().Be(ErrorType.Forbidden);
        admin.IsMfaLockedOut(_now).Should().BeTrue();
    }
}
