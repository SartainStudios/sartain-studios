using System.Net.Mail;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using EmailSettings = SartainStudios.Api.Schema.AppSettings.Email;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using OrganizationSummary = SartainStudios.Schema.Authentication.Organization;
using UserSummary = SartainStudios.Schema.Authentication.User;

namespace SartainStudios.Api.Service.Authentication;

public sealed class AuthenticationService(
    Database database,
    Token token,
    Password password,
    Session session,
    Provisioning provisioning,
    CurrentTenant currentTenant,
    IGoogleIdentityValidator googleIdentityValidator,
    IEmail emailSender,
    EmailSettings emailSettings,
    ClientSettings clientSettings,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<Response>> GoogleSignInAsync(
        GoogleSignInRequest request,
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
        var now = Now;
        var identity = await FindIdentityBySubjectAsync(
            IdentityProvider.Google, googleIdentity.Subject, cancellationToken);
        return identity is null
            ? await RegisterGoogleUserAsync(googleIdentity, request, now, cancellationToken)
            : await SignInGoogleUserAsync(identity, googleIdentity, request, now, cancellationToken);
    }

    public async Task<Result<Response>> RegisterAsync(
        EmailRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCredentials(request.Email, request.Password, true);
        if (validation.IsFailure)
            return validation.Error;
        var (email, plainTextPassword) = validation.Value;
        if (await FindIdentityBySubjectAsync(IdentityProvider.Email, email, cancellationToken) is not null)
            return AuthenticationErrors.EmailAlreadyRegistered;
        var conflict = await FindConflictingIdentityAsync(email, IdentityProvider.Email, cancellationToken);
        if (conflict is not null)
            return AuthenticationErrors.ProviderConflict(conflict.Provider);
        var now = Now;
        var user = new UserProfile
        {
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName.Trim(),
            IsAdministrator = false,
            UpdatedAt = now
        };
        await database.UserProfiles.InsertOneAsync(user, cancellationToken: cancellationToken);
        await password.CreateEmailIdentityAsync(user.Id, email, false, now);
        await password.SetPasswordAsync(user.Id, plainTextPassword, now);
        await provisioning.LinkPendingInvitesAsync(user, email, now);
        var (membership, organization) = await CreateWorkspaceAsync(user, request, email, now);
        return await IssueAsync(user, membership, organization, IdentityProvider.Email, true);
    }

    public async Task<Result<Response>> SignInAsync(
        EmailSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCredentials(request.Email, request.Password, false);
        if (validation.IsFailure)
            return validation.Error;
        var (email, plainTextPassword) = validation.Value;
        var identity = await FindIdentityBySubjectAsync(IdentityProvider.Email, email, cancellationToken);
        var credential = identity is null ? null : await password.FindCredentialAsync(identity.UserId);
        if (identity is null || credential is null)
        {
            var conflict = await FindConflictingIdentityAsync(email, IdentityProvider.Email, cancellationToken);
            return conflict is not null
                ? AuthenticationErrors.ProviderConflict(conflict.Provider)
                : AuthenticationErrors.InvalidCredentials;
        }

        if (!Password.Verify(plainTextPassword, credential.PasswordHash))
            return AuthenticationErrors.InvalidCredentials;
        var user = await FindUserAsync(identity.UserId, cancellationToken);
        if (user is null)
            return AuthenticationErrors.SessionExpired;
        var now = Now;
        await provisioning.LinkPendingInvitesAsync(user, email, now);
        var workspace = await ResolveWorkspaceAsync(user, identity.Email ?? email, now, cancellationToken);
        return await IssueAsync(
            user, workspace.Membership, workspace.Organization, IdentityProvider.Email, false);
    }

    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateEmail(request.Email);
        if (validation.IsFailure)
            return validation.Error;
        var email = validation.Value;
        var identity = await FindIdentityBySubjectAsync(IdentityProvider.Email, email, cancellationToken);
        if (identity is null)
            return Result.Success();
        var resetToken = token.CreatePasswordResetToken();
        await database.PasswordResetTokens.InsertOneAsync(
            new PasswordResetToken
            {
                UserId = identity.UserId,
                TokenHash = token.HashPasswordResetToken(resetToken),
                ExpiresAt = token.GetPasswordResetTokenExpiration()
            },
            cancellationToken: cancellationToken);
        var resetLink = PasswordResetEmail.BuildResetLink(clientSettings.BaseUrl, resetToken);
        try
        {
            emailSender.SendEmail(PasswordResetEmail.Build(email, emailSettings.Sender, resetLink));
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            return AuthenticationErrors.ResetEmailNotSent;
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Token))
            errors.Add((AuthenticationErrors.TokenField, AuthenticationErrors.ResetTokenRequired));
        if (!Password.MeetsPolicy(request.NewPassword))
            errors.Add((AuthenticationErrors.NewPasswordField,
                AuthenticationErrors.PasswordTooShort(Password.MinimumLength)));
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        var tokenHash = token.HashPasswordResetToken(request.Token);
        var now = Now;
        var resetToken = await database.PasswordResetTokens
            .Find(candidate => candidate.TokenHash == tokenHash
                               && candidate.UsedAt == null
                               && candidate.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);
        if (resetToken is null)
            return AuthenticationErrors.ResetLinkInvalid;
        var user = await FindUserAsync(resetToken.UserId, cancellationToken);
        if (user is null)
            return AuthenticationErrors.ResetLinkInvalid;
        await password.SetPasswordAsync(user.Id, request.NewPassword, now);
        resetToken.UsedAt = now;
        resetToken.UpdatedAt = now;
        await database.PasswordResetTokens.ReplaceOneAsync(
            existing => existing.Id == resetToken.Id, resetToken, cancellationToken: cancellationToken);
        await database.PasswordResetTokens.UpdateManyAsync(
            other => other.UserId == user.Id && other.UsedAt == null && other.Id != resetToken.Id,
            Builders<PasswordResetToken>.Update.Set(x => x.UsedAt, now).Set(x => x.UpdatedAt, now),
            cancellationToken: cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Response>> RefreshAsync(
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
        var organization = await database.Organizations
            .Find(candidate => candidate.Id == authenticationSession.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        var membership = await FindActiveMembershipAsync(
            authenticationSession.UserId, authenticationSession.OrganizationId, cancellationToken);
        if (user is null || organization is null || membership is null)
            return AuthenticationErrors.SessionExpired;
        await session.RevokeAsync(authenticationSession.Id);
        return await IssueAsync(
            user, membership, organization, authenticationSession.Provider, false);
    }

    public async Task<Result> SignOutAsync(SignOutRequest request, CancellationToken cancellationToken = default)
    {
        await session.RevokeAsync(currentTenant.SessionId);
        await session.RevokeByRefreshTokenAsync(request.RefreshToken);
        return Result.Success();
    }

    public Result<CurrentUserResponse> GetCurrentUser()
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var organizationId))
            return TenantErrors.NotResolved;
        return new CurrentUserResponse(
            userId.ToString(),
            organizationId.ToString(),
            currentTenant.DisplayName,
            currentTenant.Email,
            currentTenant.Role);
    }

    private async Task<Result<Response>> RegisterGoogleUserAsync(
        GoogleIdentity googleIdentity,
        GoogleSignInRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var conflict = await FindConflictingIdentityAsync(
            googleIdentity.Email, IdentityProvider.Google, cancellationToken);
        if (conflict is not null)
            return AuthenticationErrors.ProviderConflict(conflict.Provider);
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
        await provisioning.LinkPendingInvitesAsync(user, googleIdentity.Email, now);
        var (membership, organization) = await CreateWorkspaceAsync(user, request, googleIdentity.Email, now);
        return await IssueAsync(user, membership, organization, IdentityProvider.Google, true);
    }

    private async Task<Result<Response>> SignInGoogleUserAsync(
        AuthenticationIdentity identity,
        GoogleIdentity googleIdentity,
        GoogleSignInRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
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

        await provisioning.LinkPendingInvitesAsync(user, googleIdentity.Email, now);
        var workspace = await ResolveWorkspaceAsync(user, googleIdentity.Email, now, cancellationToken, request);
        return await IssueAsync(
            user, workspace.Membership, workspace.Organization, IdentityProvider.Google, false);
    }

    private async Task<Workspace> ResolveWorkspaceAsync(
        UserProfile user,
        string membershipEmail,
        DateTime now,
        CancellationToken cancellationToken,
        GoogleSignInRequest? request = null)
    {
        var membership = await database.Memberships
            .Find(candidate => candidate.UserId == user.Id && candidate.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            var (created, organization) = await provisioning.CreateOrganizationAsync(
                user,
                request?.OrganizationName,
                request?.OrganizationAddress,
                request?.OrganizationEmail,
                request?.OrganizationPhoneNumber,
                membershipEmail,
                now);
            return new Workspace(created, organization);
        }

        var existingOrganization = await database.Organizations
                                       .Find(candidate => candidate.Id == membership.OrganizationId)
                                       .FirstOrDefaultAsync(cancellationToken)
                                   ?? throw new InvalidOperationException(
                                       $"Organization {membership.OrganizationId} is missing for active membership {membership.Id}.");
        return new Workspace(membership, existingOrganization);
    }

    private async Task<Workspace> CreateWorkspaceAsync(
        UserProfile user,
        GoogleSignInRequest request,
        string membershipEmail,
        DateTime now)
    {
        var (membership, organization) = await provisioning.CreateOrganizationAsync(
            user,
            request.OrganizationName,
            request.OrganizationAddress,
            request.OrganizationEmail,
            request.OrganizationPhoneNumber,
            membershipEmail,
            now);
        return new Workspace(membership, organization);
    }

    private async Task<Workspace> CreateWorkspaceAsync(
        UserProfile user,
        EmailRegisterRequest request,
        string membershipEmail,
        DateTime now)
    {
        var (membership, organization) = await provisioning.CreateOrganizationAsync(
            user,
            request.OrganizationName,
            request.OrganizationAddress,
            request.OrganizationEmail,
            request.OrganizationPhoneNumber,
            membershipEmail,
            now);
        return new Workspace(membership, organization);
    }

    private async Task<Response> IssueAsync(
        UserProfile user,
        MembershipEntity membership,
        OrganizationEntity organization,
        IdentityProvider provider,
        bool isNewUser)
    {
        var issued = await session.IssueAsync(user, membership, organization, provider);
        return new Response(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt,
            isNewUser,
            new UserSummary(user.Id.ToString(), user.DisplayName, membership.Email, user.ProfilePhotoUrl),
            new OrganizationSummary(organization.Id.ToString(), organization.Name, membership.Role));
    }

    private Task<AuthenticationIdentity?> FindIdentityBySubjectAsync(
        IdentityProvider provider,
        string providerSubject,
        CancellationToken cancellationToken)
    {
        return database.AuthenticationIdentities
            .Find(identity => identity.Provider == provider && identity.ProviderSubject == providerSubject)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<AuthenticationIdentity?> FindConflictingIdentityAsync(
        string email,
        IdentityProvider excludingProvider,
        CancellationToken cancellationToken)
    {
        return database.AuthenticationIdentities
            .Find(identity => identity.Provider != excludingProvider && identity.Email == email)
            .FirstOrDefaultAsync(cancellationToken)!;
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

    private static Result<string> ValidateEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            return ValidationError.FromErrors(
                (AuthenticationErrors.EmailField, AuthenticationErrors.EmailRequired));
        return !Contact.IsValidEmail(normalized)
            ? ValidationError.FromErrors((AuthenticationErrors.EmailField, AuthenticationErrors.EmailInvalid))
            : normalized;
    }

    private static Result<Credentials> ValidateCredentials(
        string? email,
        string? plainTextPassword,
        bool requirePasswordPolicy)
    {
        var errors = new List<(string Field, string Message)>();
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            errors.Add((AuthenticationErrors.EmailField, AuthenticationErrors.EmailRequired));
        else if (!Contact.IsValidEmail(normalizedEmail))
            errors.Add((AuthenticationErrors.EmailField, AuthenticationErrors.EmailInvalid));
        if (string.IsNullOrWhiteSpace(plainTextPassword))
            errors.Add((AuthenticationErrors.PasswordField, AuthenticationErrors.PasswordRequired));
        else if (requirePasswordPolicy && !Password.MeetsPolicy(plainTextPassword))
            errors.Add((AuthenticationErrors.PasswordField,
                AuthenticationErrors.PasswordTooShort(Password.MinimumLength)));
        return errors.Count > 0
            ? ValidationError.FromErrors([.. errors])
            : new Credentials(normalizedEmail, plainTextPassword!);
    }

    private readonly record struct Workspace(MembershipEntity Membership, OrganizationEntity Organization);

    private sealed record Credentials(string Email, string PlainTextPassword);
}