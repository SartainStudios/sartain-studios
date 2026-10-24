using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using UserSummary = SartainStudios.Schema.Authentication.User;

namespace SartainStudios.Api.Service.Authentication;

public sealed class GigAuthenticationService(
    Database database,
    Session session,
    IGoogleIdentityValidator googleIdentityValidator,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<GigResponse>> GoogleSignInAsync(
        GigGoogleSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return ValidationError.FromErrors(
                (AuthenticationErrors.IdTokenField, AuthenticationErrors.IdTokenRequired));
        var googleIdentity = await googleIdentityValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (googleIdentity is null)
            return AuthenticationErrors.GoogleTokenInvalid;
        if (!googleIdentity.EmailVerified)
            return AuthenticationErrors.GoogleEmailUnverified;
        if (string.IsNullOrWhiteSpace(googleIdentity.Email))
            return AuthenticationErrors.GoogleEmailMissing;
        var identity = await database.AuthenticationIdentities
            .Find(candidate => candidate.Provider == IdentityProvider.Google
                               && candidate.ProviderSubject == googleIdentity.Subject)
            .FirstOrDefaultAsync(cancellationToken);
        return identity is null
            ? await RegisterAsync(googleIdentity, cancellationToken)
            : await SignInExistingAsync(identity, googleIdentity, cancellationToken);
    }

    public async Task<Result<GigResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ValidationError.FromErrors(
                (AuthenticationErrors.RefreshTokenField, AuthenticationErrors.RefreshTokenRequired));
        var authenticationSession = await session.FindActiveByRefreshTokenAsync(request.RefreshToken);
        if (authenticationSession is null)
            return AuthenticationErrors.RefreshTokenInvalid;
        var user = await FindUserAsync(authenticationSession.UserId, cancellationToken);
        if (user is null)
            return AuthenticationErrors.SessionExpired;
        if (authenticationSession.OrganizationId == ObjectId.Empty)
        {
            var email = await FindEmailAsync(user.Id, authenticationSession.Provider, cancellationToken);
            await session.RevokeAsync(authenticationSession.Id);
            return await IssueAsync(user, email, authenticationSession.Provider, false);
        }

        var membership = await FindActiveMembershipAsync(
            authenticationSession.UserId, authenticationSession.OrganizationId, cancellationToken);
        var organization = await database.Organizations
            .Find(candidate => candidate.Id == authenticationSession.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null || organization is null)
            return AuthenticationErrors.SessionExpired;
        await session.RevokeAsync(authenticationSession.Id);
        return await IssueWithOrganizationAsync(
            user, membership, organization, authenticationSession.Provider, false);
    }

    private async Task<Result<GigResponse>> RegisterAsync(
        GoogleIdentity googleIdentity,
        CancellationToken cancellationToken)
    {
        var conflict = await database.AuthenticationIdentities
            .Find(candidate => candidate.Provider != IdentityProvider.Google
                               && candidate.Email == googleIdentity.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (conflict is not null)
            return AuthenticationErrors.ProviderConflict(conflict.Provider);
        var now = Now;
        var user = new UserProfile
        {
            DisplayName = googleIdentity.DisplayName,
            ProfilePhotoUrl = googleIdentity.ProfilePhotoUrl,
            IsAdministrator = false,
            UpdatedAt = now
        };
        await database.UserProfiles.InsertOneAsync(user, cancellationToken: cancellationToken);
        await database.AuthenticationIdentities.InsertOneAsync(
            new AuthenticationIdentity
            {
                UserId = user.Id,
                Provider = IdentityProvider.Google,
                ProviderSubject = googleIdentity.Subject,
                Email = googleIdentity.Email,
                EmailVerified = true,
                UpdatedAt = now
            },
            cancellationToken: cancellationToken);
        return await IssueAsync(user, googleIdentity.Email, IdentityProvider.Google, true);
    }

    private async Task<Result<GigResponse>> SignInExistingAsync(
        AuthenticationIdentity identity,
        GoogleIdentity googleIdentity,
        CancellationToken cancellationToken)
    {
        var now = Now;
        identity.Email = googleIdentity.Email;
        identity.EmailVerified = true;
        identity.UpdatedAt = now;
        await database.AuthenticationIdentities.ReplaceOneAsync(
            existing => existing.Id == identity.Id, identity, cancellationToken: cancellationToken);
        var user = await FindUserAsync(identity.UserId, cancellationToken);
        if (user is null)
            return AuthenticationErrors.SessionExpired;
        if (!string.Equals(user.ProfilePhotoUrl, googleIdentity.ProfilePhotoUrl, StringComparison.Ordinal))
        {
            user.ProfilePhotoUrl = googleIdentity.ProfilePhotoUrl;
            user.UpdatedAt = now;
            await database.UserProfiles.ReplaceOneAsync(
                existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
        }

        var membership = await database.Memberships
            .Find(candidate => candidate.UserId == user.Id && candidate.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
            return await IssueAsync(user, googleIdentity.Email, IdentityProvider.Google, false);
        var organization = await database.Organizations
            .Find(candidate => candidate.Id == membership.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        return organization is null
            ? await IssueAsync(user, googleIdentity.Email, IdentityProvider.Google, false)
            : await IssueWithOrganizationAsync(user, membership, organization, IdentityProvider.Google, false);
    }

    private async Task<GigResponse> IssueAsync(
        UserProfile user,
        string email,
        IdentityProvider provider,
        bool isNewUser)
    {
        var issued = await session.IssueGigAsync(user, email, provider);
        return ToResponse(issued, isNewUser, user, email);
    }

    private async Task<GigResponse> IssueWithOrganizationAsync(
        UserProfile user,
        MembershipEntity membership,
        SartainStudios.Schema.DatabaseEntity.Organization organization,
        IdentityProvider provider,
        bool isNewUser)
    {
        var issued = await session.IssueAsync(user, membership, organization, provider);
        return ToResponse(issued, isNewUser, user, membership.Email);
    }

    private static GigResponse ToResponse(IssuedSession issued, bool isNewUser, UserProfile user, string email)
    {
        return new GigResponse(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt,
            isNewUser,
            new UserSummary(user.Id.ToString(), user.DisplayName, email, user.ProfilePhotoUrl));
    }

    private async Task<string> FindEmailAsync(
        ObjectId userId,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        var identity = await database.AuthenticationIdentities
            .Find(candidate => candidate.UserId == userId && candidate.Provider == provider)
            .FirstOrDefaultAsync(cancellationToken);
        return identity?.Email ?? string.Empty;
    }

    private Task<UserProfile?> FindUserAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        return database.UserProfiles
            .Find(user => user.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<MembershipEntity?> FindActiveMembershipAsync(
        ObjectId userId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Memberships
            .Find(membership => membership.UserId == userId
                                && membership.OrganizationId == organizationId
                                && membership.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync(cancellationToken)!;
    }
}