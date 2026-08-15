using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.User;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using UserSummary = SartainStudios.Schema.Authentication.User;

namespace SartainStudios.Api.Service.Account;

public sealed class AccountService(
    Database database,
    CurrentTenant currentTenant,
    Password password,
    Deletion deletion,
    TimeProvider timeProvider)
{
    public async Task<Result<AccountResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(cancellationToken);
        return context.IsFailure
            ? context.Error
            : await BuildResponseAsync(context.Value, cancellationToken);
    }

    public async Task<Result<AccountResponse>> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProfile(request.DisplayName, request.ProfilePhotoUrl);
        if (validation.IsFailure)
            return validation.Error;
        var context = await LoadContextAsync(cancellationToken);
        if (context.IsFailure)
            return context.Error;
        var (user, _) = context.Value;
        var details = validation.Value;
        user.DisplayName = details.DisplayName;
        user.ProfilePhotoUrl = details.ProfilePhotoUrl;
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.UserProfiles.ReplaceOneAsync(
            existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
        return await BuildResponseAsync(context.Value, cancellationToken);
    }

    public async Task<Result<AccountResponse>> UpdateNotificationPreferencesAsync(
        NotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateNotificationPreferences(
            request.WeeklyHourLimitMinutes, request.HourLimitWarningMinutes);
        if (validation.IsFailure)
            return validation.Error;
        var context = await LoadContextAsync(cancellationToken);
        if (context.IsFailure)
            return context.Error;
        var membership = context.Value.Membership;
        membership.WeeklyHourLimitMinutes = request.WeeklyHourLimitMinutes;
        membership.HourLimitWarningMinutes = request.HourLimitWarningMinutes;
        membership.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Memberships.ReplaceOneAsync(
            existing => existing.Id == membership.Id, membership, cancellationToken: cancellationToken);
        return await BuildResponseAsync(context.Value, cancellationToken);
    }

    public async Task<Result<AccountResponse>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Password.MeetsPolicy(request.NewPassword))
            return ValidationError.FromErrors(
                (AccountErrors.NewPasswordField, AccountErrors.NewPasswordTooShort(Password.MinimumLength)));
        var context = await LoadContextAsync(cancellationToken);
        if (context.IsFailure)
            return context.Error;
        var (user, membership) = context.Value;
        var credential = await password.FindCredentialAsync(user.Id);
        var change = credential is not null
            ? await ReplacePasswordAsync(credential, request)
            : await AddPasswordAsync(user, membership, request);
        return change.IsFailure
            ? change.Error
            : await BuildResponseAsync(context.Value, cancellationToken);
    }

    public async Task<Result<AccountResponse>> UnlinkIdentityAsync(
        IdentityProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(provider))
            return ValidationError.FromErrors((AccountErrors.ProviderField, AccountErrors.ProviderUnknown));
        var context = await LoadContextAsync(cancellationToken);
        if (context.IsFailure)
            return context.Error;
        var user = context.Value.User;
        var identities = await LoadIdentitiesAsync(user.Id, cancellationToken);
        var target = identities.FirstOrDefault(identity => identity.Provider == provider);
        if (target is null)
            return AccountErrors.IdentityNotLinked;
        if (identities.Count <= 1)
            return AccountErrors.LastSignInMethod;
        await database.AuthenticationIdentities.DeleteOneAsync(
            identity => identity.Id == target.Id, cancellationToken);
        if (target.Provider == IdentityProvider.Email)
            await database.EmailPasswordCredentials.DeleteManyAsync(
                credential => credential.UserId == target.UserId, cancellationToken);
        return await BuildResponseAsync(context.Value, cancellationToken);
    }

    public async Task<Result> DeleteAsync(string userTimeZoneId, CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);

        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        var outcome = await deletion.DeleteUserAsync(userId, userTimeZone);
        return outcome switch
        {
            DeletionOutcome.Deleted => Result.Success(),
            DeletionOutcome.UserNotFound => AccountErrors.NotResolved,
            _ => AccountErrors.DeletionConflict
        };
    }

    private async Task<Result<AccountContext>> LoadContextAsync(CancellationToken cancellationToken)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var organizationId))
            return TenantErrors.NotResolved;
        var user = await database.UserProfiles
            .Find(profile => profile.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return AccountErrors.NotResolved;
        var membership = await database.Memberships
            .Find(member => member.UserId == userId
                            && member.OrganizationId == organizationId
                            && member.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);
        return membership is null
            ? AccountErrors.NotResolved
            : new AccountContext(user, membership);
    }

    private async Task<Result> ReplacePasswordAsync(
        EmailPasswordCredential credential,
        ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return ValidationError.FromErrors(
                (AccountErrors.CurrentPasswordField, AccountErrors.CurrentPasswordRequired));
        if (!Password.Verify(request.CurrentPassword, credential.PasswordHash))
            return ValidationError.FromErrors(
                (AccountErrors.CurrentPasswordField, AccountErrors.CurrentPasswordIncorrect));
        if (Password.Verify(request.NewPassword, credential.PasswordHash))
            return ValidationError.FromErrors(
                (AccountErrors.NewPasswordField, AccountErrors.NewPasswordMatchesCurrent));
        await password.SetPasswordAsync(credential.UserId, request.NewPassword,
            timeProvider.GetUtcNow().UtcDateTime);
        return Result.Success();
    }

    private async Task<Result> AddPasswordAsync(
        UserProfile user,
        MembershipEntity membership,
        ChangePasswordRequest request)
    {
        if (await password.EmailIdentityExistsAsync(membership.Email))
            return AccountErrors.EmailAlreadyUsed;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await password.SetPasswordAsync(user.Id, request.NewPassword, now);
        await password.CreateEmailIdentityAsync(user.Id, membership.Email, false, now);
        return Result.Success();
    }

    private Task<List<AuthenticationIdentity>> LoadIdentitiesAsync(
        ObjectId userId,
        CancellationToken cancellationToken)
    {
        return database.AuthenticationIdentities
            .Find(identity => identity.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    private async Task<AccountResponse> BuildResponseAsync(
        AccountContext context,
        CancellationToken cancellationToken)
    {
        var (user, membership) = context;
        var identities = await LoadIdentitiesAsync(user.Id, cancellationToken);
        var hasPassword = await password.HasPasswordAsync(user.Id);
        return new AccountResponse(
            new UserSummary(user.Id.ToString(), user.DisplayName, membership.Email, user.ProfilePhotoUrl),
            user.IsAdministrator,
            identities
                .OrderBy(identity => identity.Provider)
                .Select(identity => new LinkedIdentityResponse(
                    identity.Provider, identity.Email, identity.EmailVerified, identity.CreatedAt))
                .ToList(),
            hasPassword,
            membership.WeeklyHourLimitMinutes,
            membership.HourLimitWarningMinutes);
    }

    private static Result<ProfileDetails> ValidateProfile(string? displayName, string? profilePhotoUrl)
    {
        var errors = new List<(string Field, string Message)>();
        var trimmedDisplayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedDisplayName))
            errors.Add((AccountErrors.DisplayNameField, AccountErrors.DisplayNameRequired));
        else if (trimmedDisplayName.Length > AccountErrors.DisplayNameMaximumLength)
            errors.Add((AccountErrors.DisplayNameField, AccountErrors.DisplayNameTooLong));
        var trimmedPhotoUrl = profilePhotoUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPhotoUrl))
        {
            if (trimmedPhotoUrl.Length > AccountErrors.ProfilePhotoUrlMaximumLength)
                errors.Add((AccountErrors.ProfilePhotoUrlField, AccountErrors.ProfilePhotoUrlTooLong));
            else if (!IsHttpUrl(trimmedPhotoUrl))
                errors.Add((AccountErrors.ProfilePhotoUrlField, AccountErrors.ProfilePhotoUrlInvalid));
        }

        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new ProfileDetails(
            trimmedDisplayName,
            string.IsNullOrWhiteSpace(trimmedPhotoUrl) ? null : trimmedPhotoUrl);
    }

    private static Result ValidateNotificationPreferences(
        int? weeklyHourLimitMinutes,
        int hourLimitWarningMinutes)
    {
        var errors = new List<(string Field, string Message)>();
        if (weeklyHourLimitMinutes is < 1 or > AccountErrors.WeeklyHourLimitMaximumMinutes)
            errors.Add((AccountErrors.WeeklyHourLimitMinutesField, AccountErrors.WeeklyHourLimitOutOfRange));
        if (hourLimitWarningMinutes < 0)
            errors.Add((AccountErrors.HourLimitWarningMinutesField, AccountErrors.HourLimitWarningNegative));
        else if (weeklyHourLimitMinutes is { } limit && hourLimitWarningMinutes > limit)
            errors.Add((AccountErrors.HourLimitWarningMinutesField, AccountErrors.HourLimitWarningExceedsLimit));
        return errors.Count > 0
            ? ValidationError.FromErrors([.. errors])
            : Result.Success();
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private readonly record struct AccountContext(UserProfile User, MembershipEntity Membership);

    private sealed record ProfileDetails(string DisplayName, string? ProfilePhotoUrl);
}