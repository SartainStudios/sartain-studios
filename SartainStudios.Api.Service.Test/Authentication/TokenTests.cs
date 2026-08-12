using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MongoDB.Bson;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class TokenTests
{
    private static readonly Jwt JwtSettings = new()
    {
        Issuer = "issuer-test",
        Audience = "audience-test",
        SigningKey = "this-is-a-long-signing-key-for-tests",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7
    };

    [Fact]
    public void CreateAccessToken_ContainsExpectedClaimsAndExpiration()
    {
        var service = new Token(JwtSettings);
        var user = new UserProfile { DisplayName = "Test User" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id, OrganizationId = ObjectId.GenerateNewId(), Email = "user@example.com", Role = "Owner",
            Status = "Active"
        };
        var organization = new OrganizationEntity { Name = "Acme" };
        var authenticationSession = new AuthenticationSession
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Provider = IdentityProvider.Email,
            RefreshTokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var result = service.CreateAccessToken(user, membership, organization, authenticationSession);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value);

        Assert.Equal(JwtSettings.Issuer, token.Issuer);
        Assert.Contains(JwtSettings.Audience, token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Test User", token.Claims.First(x => x.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("user@example.com", token.Claims.First(x => x.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(organization.Id.ToString(),
            token.Claims.First(x => x.Type == nameof(JwtClaimName.OrganizationId)).Value);
        Assert.Equal("Owner", token.Claims.First(x => x.Type == ClaimTypes.Role).Value);
        Assert.Equal(authenticationSession.Id.ToString(),
            token.Claims.First(x => x.Type == nameof(JwtClaimName.SessionId)).Value);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CreateRefreshToken_ReturnsRandomNonEmptyToken()
    {
        var service = new Token(JwtSettings);

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashRefreshToken_IsDeterministicSha256Hex()
    {
        var service = new Token(JwtSettings);

        var first = service.HashRefreshToken("refresh-token");
        var second = service.HashRefreshToken("refresh-token");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[A-F0-9]{64}$", first);
    }

    [Fact]
    public void GetRefreshTokenExpiration_UsesConfiguredDays()
    {
        var service = new Token(JwtSettings);
        var before = DateTime.UtcNow;

        var expiration = service.GetRefreshTokenExpiration();

        var after = DateTime.UtcNow;
        Assert.InRange(expiration, before.AddDays(JwtSettings.RefreshTokenDays),
            after.AddDays(JwtSettings.RefreshTokenDays));
    }

    [Fact]
    public void PasswordResetTokenHelpers_WorkAsExpected()
    {
        var service = new Token(JwtSettings);

        var token = service.CreatePasswordResetToken();
        var hash = service.HashPasswordResetToken(token);
        var expiration = service.GetPasswordResetTokenExpiration();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[A-F0-9]{64}$", hash);
        Assert.InRange(expiration, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }
}