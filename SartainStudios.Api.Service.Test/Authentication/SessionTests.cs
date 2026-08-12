using MongoDB.Bson;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class SessionTests
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
    public async Task IssueAsync_PersistsSessionAndReturnsTokens()
    {
        var harness = new MongoHarness();
        var token = new Token(JwtSettings);
        var service = new Session(harness.Database, token, new StaticTimeProvider(DateTime.UtcNow));
        var user = new UserProfile { DisplayName = "Session User" };
        var organization = new OrganizationEntity { Name = "Acme" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = "Owner",
            Status = "Active",
            Email = "session@example.com"
        };

        var issued = await service.IssueAsync(user, membership, organization, IdentityProvider.Email);

        Assert.False(string.IsNullOrWhiteSpace(issued.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(issued.RefreshToken));
        Assert.True(issued.RefreshTokenExpiresAt > DateTime.UtcNow);
        var stored = Assert.Single(harness.AuthenticationSessions.Documents);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(organization.Id, stored.OrganizationId);
        Assert.Equal(IdentityProvider.Email, stored.Provider);
        Assert.Equal(token.HashRefreshToken(issued.RefreshToken), stored.RefreshTokenHash);
    }

    [Fact]
    public async Task FindActiveByRefreshTokenAsync_ReturnsOnlyUnrevokedUnexpiredSession()
    {
        var harness = new MongoHarness();
        var token = new Token(JwtSettings);
        var service = new Session(harness.Database, token, new StaticTimeProvider(DateTime.UtcNow));
        var refreshToken = "refresh-token";
        var active = new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            RefreshTokenHash = token.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        var revoked = new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            RefreshTokenHash = token.HashRefreshToken("revoked"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            RevokedAt = DateTime.UtcNow
        };
        var expired = new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            RefreshTokenHash = token.HashRefreshToken("expired"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        harness.AuthenticationSessions.Seed(active, revoked, expired);

        var found = await service.FindActiveByRefreshTokenAsync(refreshToken);
        var notFound = await service.FindActiveByRefreshTokenAsync("missing");

        Assert.NotNull(found);
        Assert.Equal(active.Id, found.Id);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task RevokeAsync_DoesNothingForEmptySessionId()
    {
        var harness = new MongoHarness();
        var token = new Token(JwtSettings);
        var service = new Session(harness.Database, token, new StaticTimeProvider(DateTime.UtcNow));

        await service.RevokeAsync(ObjectId.Empty);

        Assert.Empty(harness.AuthenticationSessions.Replaced);
    }

    [Fact]
    public async Task RevokeAsync_SetsRevokedAtForMatchingSession()
    {
        var harness = new MongoHarness();
        var token = new Token(JwtSettings);
        var service = new Session(harness.Database, token, new StaticTimeProvider(DateTime.UtcNow));
        var session = new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Google,
            RefreshTokenHash = token.HashRefreshToken("abc"),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        harness.AuthenticationSessions.Seed(session);

        await service.RevokeAsync(session.Id);

        var stored = Assert.Single(harness.AuthenticationSessions.Documents);
        Assert.NotNull(stored.RevokedAt);
        Assert.Single(harness.AuthenticationSessions.Replaced);
    }

    [Fact]
    public async Task RevokeByRefreshTokenAsync_RevokesMatchingSession()
    {
        var harness = new MongoHarness();
        var token = new Token(JwtSettings);
        var service = new Session(harness.Database, token, new StaticTimeProvider(DateTime.UtcNow));
        var rawRefreshToken = "raw-refresh-token";
        var session = new AuthenticationSession
        {
            UserId = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            RefreshTokenHash = token.HashRefreshToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        harness.AuthenticationSessions.Seed(session);

        await service.RevokeByRefreshTokenAsync(rawRefreshToken);

        var stored = Assert.Single(harness.AuthenticationSessions.Documents);
        Assert.NotNull(stored.RevokedAt);
    }
}