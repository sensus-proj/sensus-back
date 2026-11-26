using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Authentication;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    public const string SecurityStampClaimType = "security_stamp";
    public const string AmrClaimType = "amr";
    public const string MfaVerifiedAtClaimType = "mfa_at";
    public const string TokenUseClaimType = "token_use";
    public const string MfaChallengeTokenUse = "mfa_challenge";

    private readonly JwtSettings _jwtSettings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly MfaSettings _mfaSettings;

    public JwtTokenGenerator(
        IOptions<JwtSettings> jwtOptions,
        IDateTimeProvider dateTimeProvider,
        IOptions<MfaSettings>? mfaOptions = null)
    {
        _jwtSettings = jwtOptions.Value;
        _dateTimeProvider = dateTimeProvider;
        _mfaSettings = mfaOptions?.Value ?? new MfaSettings();
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        var now = _dateTimeProvider.UtcNow;
        var expiresAtUtc = now.AddMinutes(_jwtSettings.ExpiryMinutes);
        return CreateUserToken(user, now, expiresAtUtc, extraClaims: null);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateAdminStepUpToken(User user, DateTime mfaVerifiedAtUtc)
    {
        var now = _dateTimeProvider.UtcNow;
        var expiresAtUtc = now.AddMinutes(_jwtSettings.ExpiryMinutes);
        var verifiedUtc = mfaVerifiedAtUtc.Kind == DateTimeKind.Utc
            ? mfaVerifiedAtUtc
            : DateTime.SpecifyKind(mfaVerifiedAtUtc, DateTimeKind.Utc);
        var unixSeconds = new DateTimeOffset(verifiedUtc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        return CreateUserToken(
            user,
            now,
            expiresAtUtc,
            [
                new Claim(AmrClaimType, "mfa"),
                new Claim(MfaVerifiedAtClaimType, unixSeconds)
            ]);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateMfaChallengeToken(User user)
    {
        var now = _dateTimeProvider.UtcNow;
        var challengeMinutes = _mfaSettings.ChallengeExpiryMinutes is >= 1 and <= 15
            ? _mfaSettings.ChallengeExpiryMinutes
            : 5;
        var expiresAtUtc = now.AddMinutes(challengeMinutes);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SecurityStampClaimType, user.SecurityStamp),
            new(TokenUseClaimType, MfaChallengeTokenUse)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expiresAtUtc,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(securityToken), expiresAtUtc);
    }

    private (string Token, DateTime ExpiresAtUtc) CreateUserToken(
        User user,
        DateTime now,
        DateTime expiresAtUtc,
        IReadOnlyList<Claim>? extraClaims)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Correo),
            new(JwtRegisteredClaimNames.Name, user.NombreCompleto),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SecurityStampClaimType, user.SecurityStamp)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expiresAtUtc,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(securityToken), expiresAtUtc);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateGuestToken(Guid? guestId = null, int? expiryMinutes = null)
    {
        var id = guestId ?? Guid.NewGuid();
        var configuredMinutes = _jwtSettings.GuestExpiryMinutes is >= 1 and <= 15
            ? _jwtSettings.GuestExpiryMinutes
            : 15;
        var minutes = expiryMinutes ?? configuredMinutes;
        var now = _dateTimeProvider.UtcNow;
        var expiresAtUtc = now.AddMinutes(minutes);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id.ToString()),
            new(JwtRegisteredClaimNames.Name, "Invitado"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, RoleType.Invitado)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expiresAtUtc,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(securityToken);

        return (tokenString, expiresAtUtc);
    }
}
