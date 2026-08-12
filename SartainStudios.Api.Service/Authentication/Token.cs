using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Authentication;

public sealed class Token(Jwt jwtSettings)
{
    public TokenResult CreateAccessToken(
        UserProfile user,
        SartainStudios.Schema.DatabaseEntity.Membership membership,
        SartainStudios.Schema.DatabaseEntity.Organization organization,
        AuthenticationSession session)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(jwtSettings.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Email, membership.Email),
            new Claim(nameof(JwtClaimName.OrganizationId), organization.Id.ToString()),
            new Claim(ClaimTypes.Role, membership.Role),
            new Claim(nameof(JwtClaimName.SessionId), session.Id.ToString())
        };

        var credentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);
        var descriptor = new JwtSecurityToken(
            jwtSettings.Issuer,
            jwtSettings.Audience,
            claims,
            now,
            expiresAt,
            credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(descriptor);
        return new TokenResult(accessToken, expiresAt);
    }

    public string CreateRefreshToken()
    {
        return CreateSecureToken();
    }

    public string HashRefreshToken(string refreshToken)
    {
        return HashSecureToken(refreshToken);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        return DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenDays);
    }

    public string CreatePasswordResetToken()
    {
        return CreateSecureToken();
    }

    public string HashPasswordResetToken(string resetToken)
    {
        return HashSecureToken(resetToken);
    }

    public DateTime GetPasswordResetTokenExpiration()
    {
        return DateTime.UtcNow.AddHours(1);
    }

    private static string CreateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashSecureToken(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var key = Encoding.UTF8.GetBytes(jwtSettings.SigningKey);
        return new SymmetricSecurityKey(key);
    }
}