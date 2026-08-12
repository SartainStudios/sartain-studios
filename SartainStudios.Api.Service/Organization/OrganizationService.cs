using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.Organization;
using CreateRequest = SartainStudios.Schema.Organization.CreateRequest;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using UpdateRequest = SartainStudios.Schema.Organization.UpdateRequest;

namespace SartainStudios.Api.Service.Organization;

public sealed class OrganizationService(
    Database database,
    CurrentTenant currentTenant,
    Lookup lookup,
    Provisioning provisioning,
    Session session,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<OrganizationSummary>>> ListMineAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var activeOrganizationId))
            return TenantErrors.NotResolved;

        var memberships = await database.Memberships
            .Find<MembershipEntity>(membership =>
                membership.UserId == userId && membership.Status != nameof(RoleStatus.Suspended))
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
            return Result.Success<IReadOnlyList<OrganizationSummary>>([]);

        var organizations = await lookup.OrganizationsAsync(memberships.Select(m => m.OrganizationId));

        IReadOnlyList<OrganizationSummary> summaries = memberships
            .Where(m => organizations.ContainsKey(m.OrganizationId))
            .Select(m => ToSummary(organizations[m.OrganizationId], m, activeOrganizationId))
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .ToList();

        return Result.Success(summaries);
    }

    public async Task<Result<OrganizationSummary>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var activeOrganizationId))
            return TenantErrors.NotResolved;

        if (!ObjectId.TryParse(id, out var organizationId))
            return OrganizationErrors.InvalidId;

        var membership = await FindActiveMembershipAsync(organizationId, userId, cancellationToken);

        if (membership is null)
            return OrganizationErrors.Forbidden;

        var organization = await FindOrganizationAsync(organizationId, cancellationToken);

        return organization is null
            ? OrganizationErrors.NotFound(id)
            : ToSummary(organization, membership, activeOrganizationId);
    }

    public async Task<Result<OrganizationSummary>> CreateAsync(
        CreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out _))
            return TenantErrors.NotResolved;

        var validation = Validate(request.Name, request.Email, request.PhoneNumber);

        if (validation.IsFailure)
            return validation.Error;

        var user = await database.UserProfiles.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return TenantErrors.NotResolved;

        var details = validation.Value;
        var membershipEmail = details.Email ?? currentTenant.Email ?? string.Empty;

        var (membership, organization) = await provisioning.CreateOrganizationAsync(
            user, details.Name, request.Address, details.Email, details.PhoneNumber, membershipEmail);

        return ToSummary(organization, membership, ObjectId.Empty);
    }

    public async Task<Result<OrganizationSummary>> UpdateAsync(
        string id,
        UpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var activeOrganizationId))
            return TenantErrors.NotResolved;

        if (!ObjectId.TryParse(id, out var organizationId) || organizationId != activeOrganizationId)
            return OrganizationErrors.NotActiveOrganization;

        var validation = Validate(request.Name, request.Email, request.PhoneNumber);

        if (validation.IsFailure)
            return validation.Error;

        var organization = await FindOrganizationAsync(organizationId, cancellationToken);

        if (organization is null)
            return OrganizationErrors.NotFound(id);

        var details = validation.Value;

        organization.Name = details.Name;
        organization.Address = request.Address?.Trimmed() ?? new Address();
        organization.Email = details.Email ?? organization.Email;
        organization.PhoneNumber = details.PhoneNumber ?? string.Empty;
        organization.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await database.Organizations.ReplaceOneAsync<OrganizationEntity>(
            existing => existing.Id == organization.Id, organization, cancellationToken: cancellationToken);

        var membership = await database.Memberships
            .Find(m => m.OrganizationId == organizationId && m.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return new OrganizationSummary(
            organization.Id.ToString(),
            organization.Name,
            organization.Address,
            organization.Email,
            organization.PhoneNumber,
            membership?.Role ?? nameof(RoleType.Owner),
            membership?.Status ?? nameof(RoleStatus.Active),
            true);
    }

    public async Task<Result<SwitchResponse>> SwitchAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out var userId, out _))
            return TenantErrors.NotResolved;

        if (!ObjectId.TryParse(id, out var targetOrganizationId))
            return OrganizationErrors.InvalidId;

        var membership = await FindActiveMembershipAsync(targetOrganizationId, userId, cancellationToken);

        if (membership is null)
            return OrganizationErrors.Forbidden;

        var organization = await FindOrganizationAsync(targetOrganizationId, cancellationToken);

        if (organization is null)
            return OrganizationErrors.NotFound(id);

        var user = await database.UserProfiles.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return TenantErrors.NotResolved;

        await session.RevokeAsync(currentTenant.SessionId);

        var issued = await session.IssueAsync(user, membership, organization, IdentityProvider.Email);

        return new SwitchResponse(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt,
            new User(user.Id.ToString(), user.DisplayName, membership.Email, user.ProfilePhotoUrl),
            new SartainStudios.Schema.Authentication.Organization(organization.Id.ToString(), organization.Name,
                membership.Role));
    }

    private Task<OrganizationEntity?> FindOrganizationAsync(
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Organizations
            .Find<OrganizationEntity>(organization => organization.Id == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private Task<MembershipEntity?> FindActiveMembershipAsync(
        ObjectId organizationId,
        ObjectId userId,
        CancellationToken cancellationToken)
    {
        return database.Memberships
            .Find<MembershipEntity>(m => m.OrganizationId == organizationId && m.UserId == userId &&
                                         m.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private static OrganizationSummary ToSummary(
        OrganizationEntity organization,
        MembershipEntity membership,
        ObjectId activeOrganizationId)
    {
        return new OrganizationSummary(
            organization.Id.ToString(),
            organization.Name,
            organization.Address,
            organization.Email,
            organization.PhoneNumber,
            membership.Role,
            membership.Status,
            organization.Id == activeOrganizationId);
    }

    private static Result<OrganizationDetails> Validate(string? name, string? email, string? phoneNumber)
    {
        var errors = new List<(string Field, string Message)>();

        var trimmedName = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
            errors.Add((OrganizationErrors.NameField, OrganizationErrors.NameRequired));

        string? trimmedEmail = null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            trimmedEmail = email.Trim();

            if (!Contact.IsValidEmail(trimmedEmail))
                errors.Add((OrganizationErrors.EmailField, OrganizationErrors.EmailInvalid));
        }

        string? trimmedPhoneNumber = null;

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            trimmedPhoneNumber = phoneNumber.Trim();

            if (!Contact.IsValidPhoneNumber(trimmedPhoneNumber))
                errors.Add((OrganizationErrors.PhoneNumberField, OrganizationErrors.PhoneNumberInvalid));
        }

        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);

        return new OrganizationDetails(trimmedName, trimmedEmail, trimmedPhoneNumber);
    }

    private sealed record OrganizationDetails(string Name, string? Email, string? PhoneNumber);
}