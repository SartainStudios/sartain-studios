using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Authentication;

public sealed class Session(Database database, Token token)
{
    public async Task<IssuedSession> IssueAsync(UserProfile user,
        SartainStudios.Schema.DatabaseEntity.Membership membership,
        OrganizationEntity organization, IdentityProvider provider)
    {
        var refreshToken = token.CreateRefreshToken();
        var refreshTokenExpiresAt = token.GetRefreshTokenExpiration();
        var authenticationSession = new AuthenticationSession
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Provider = provider,
            RefreshTokenHash = token.HashRefreshToken(refreshToken),
            ExpiresAt = refreshTokenExpiresAt
        };
        await database.AuthenticationSessions.InsertOneAsync(authenticationSession);
        var accessToken = token.CreateAccessToken(user, membership, organization, authenticationSession);
        return new IssuedSession(accessToken.Value, accessToken.ExpiresAt, refreshToken, refreshTokenExpiresAt);
    }

    public async Task<AuthenticationSession?> FindActiveByRefreshTokenAsync(string refreshToken)
    {
        var refreshTokenHash = token.HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;
        return await database.AuthenticationSessions
            .Find(x => x.RefreshTokenHash == refreshTokenHash && x.RevokedAt == null && x.ExpiresAt > now)
            .FirstOrDefaultAsync();
    }

    public async Task RevokeAsync(ObjectId sessionId)
    {
        if (sessionId == ObjectId.Empty) return;
        var session = await database.AuthenticationSessions
            .Find(x => x.Id == sessionId)
            .FirstOrDefaultAsync();
        await RevokeAsync(session);
    }

    public async Task RevokeByRefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var refreshTokenHash = token.HashRefreshToken(refreshToken);
        var session = await database.AuthenticationSessions
            .Find(x => x.RefreshTokenHash == refreshTokenHash)
            .FirstOrDefaultAsync();
        await RevokeAsync(session);
    }

    private async Task RevokeAsync(AuthenticationSession? session)
    {
        if (session is null || session.RevokedAt is not null) return;
        var now = DateTime.UtcNow;
        session.RevokedAt = now;
        session.UpdatedAt = now;
        await database.AuthenticationSessions.ReplaceOneAsync(x => x.Id == session.Id, session);
    }
}