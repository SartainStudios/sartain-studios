using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Service.Account;

public sealed class UserAdministrationService(
    Database database,
    CurrentTenant currentTenant,
    Deletion deletion,
    TimeProvider timeProvider)
{
    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default)
    {
        return await FindAdministratorAsync(cancellationToken) is not null;
    }

    public async Task<Result<IReadOnlyList<AdministratorUserResponse>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (await FindAdministratorAsync(cancellationToken) is null)
            return AccountErrors.AdministrationNotFound;
        var users = await database.UserProfiles
            .Find(_ => true)
            .ToListAsync(cancellationToken);
        var memberships = await database.Memberships
            .Find(_ => true)
            .ToListAsync(cancellationToken);
        var emailsByUser = memberships
            .Where(membership => !string.IsNullOrWhiteSpace(membership.Email))
            .GroupBy(membership => membership.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(membership => membership.Email)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList());
        IReadOnlyList<AdministratorUserResponse> responses = users
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.CreatedAt)
            .Select(user => ToResponse(
                user,
                emailsByUser.GetValueOrDefault(user.Id) ?? []))
            .ToList();
        return Result.Success(responses);
    }

    public async Task<Result<AdministratorUserResponse>> UpdateAsync(
        string id,
        AdministratorUpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var administrator = await FindAdministratorAsync(cancellationToken);
        if (administrator is null)
            return AccountErrors.AdministrationNotFound;
        if (!ObjectId.TryParse(id, out var userId))
            return AccountErrors.UserNotFound;
        var validation = AccountService.ValidateProfile(request.DisplayName, request.ProfilePhotoUrl);
        if (validation.IsFailure)
            return validation.Error;
        var user = await database.UserProfiles
            .Find(profile => profile.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return AccountErrors.UserNotFound;
        if (user.Id == administrator.Id && !request.IsAdministrator)
            return AccountErrors.CannotModifySelf;
        var details = validation.Value;
        user.DisplayName = details.DisplayName;
        user.ProfilePhotoUrl = details.ProfilePhotoUrl;
        user.IsAdministrator = request.IsAdministrator;
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.UserProfiles.ReplaceOneAsync(
            existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
        var emails = await LoadEmailsAsync(user.Id, cancellationToken);
        return ToResponse(user, emails);
    }

    public async Task<Result> DeleteAsync(
        string id,
        string? userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var administrator = await FindAdministratorAsync(cancellationToken);
        if (administrator is null)
            return AccountErrors.AdministrationNotFound;
        if (!ObjectId.TryParse(id, out var userId))
            return AccountErrors.UserNotFound;
        if (userId == administrator.Id)
            return AccountErrors.CannotModifySelf;
        var outcome = await deletion.DeleteUserAsync(userId, ResolveTimeZone(userTimeZoneId));
        return outcome switch
        {
            DeletionOutcome.Deleted => Result.Success(),
            DeletionOutcome.UserNotFound => AccountErrors.UserNotFound,
            _ => AccountErrors.DeletionConflict
        };
    }

    private async Task<UserProfile?> FindAdministratorAsync(CancellationToken cancellationToken)
    {
        var userId = currentTenant.UserId;
        if (userId == ObjectId.Empty)
            return null;
        var user = await database.UserProfiles
            .Find(profile => profile.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return user is { IsAdministrator: true } ? user : null;
    }

    private async Task<IReadOnlyList<string>> LoadEmailsAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var memberships = await database.Memberships
            .Find(membership => membership.UserId == userId)
            .ToListAsync(cancellationToken);
        return memberships
            .Select(membership => membership.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdministratorUserResponse ToResponse(UserProfile user, IReadOnlyList<string> emails)
    {
        return new AdministratorUserResponse(
            user.Id.ToString(),
            user.DisplayName,
            user.ProfilePhotoUrl,
            emails,
            user.IsAdministrator,
            user.CreatedAt);
    }

    private static TimeZoneInfo ResolveTimeZone(string? userTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(userTimeZoneId))
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}