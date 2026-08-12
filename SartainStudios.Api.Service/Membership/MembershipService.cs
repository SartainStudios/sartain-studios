using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Membership;
using InviteRequest = SartainStudios.Schema.Membership.InviteRequest;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using Summary = SartainStudios.Schema.Membership.Summary;
using UpdateRequest = SartainStudios.Schema.Membership.UpdateRequest;

namespace SartainStudios.Api.Service.Membership;

public sealed class MembershipService(
    Database database,
    CurrentTenant currentTenant,
    Roster roster,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<Summary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var memberships = await database.Memberships
            .Find<MembershipEntity>(membership => membership.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        return Result.Success(await roster.ToSummariesAsync(memberships));
    }

    public async Task<Result<Summary>> InviteAsync(
        InviteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        var validation = ValidateInvite(request);
        if (validation.IsFailure)
            return validation.Error;
        var (email, normalizedRole) = validation.Value;
        if (normalizedRole == nameof(RoleType.Owner) && currentTenant.Role != nameof(RoleType.Owner))
            return MembershipErrors.OnlyOwnerCanGrantOwnership;
        var alreadyInvited = await database.Memberships
            .Find<MembershipEntity>(membership =>
                membership.OrganizationId == organizationId && membership.Email == email)
            .AnyAsync(cancellationToken);
        if (alreadyInvited)
            return MembershipErrors.AlreadyInvited;
        var identity = await database.AuthenticationIdentities
            .Find(authenticationIdentity => authenticationIdentity.Email == email)
            .FirstOrDefaultAsync(cancellationToken);
        var membershipEntity = new MembershipEntity
        {
            OrganizationId = organizationId,
            UserId = identity?.UserId ?? ObjectId.Empty,
            Role = normalizedRole,
            Status = nameof(RoleStatus.Invited),
            Email = email,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        if (membershipEntity.UserId != ObjectId.Empty)
        {
            var alreadyMember = await database.Memberships
                .Find(membership =>
                    membership.OrganizationId == organizationId && membership.UserId == membershipEntity.UserId)
                .AnyAsync(cancellationToken);
            if (alreadyMember)
                return MembershipErrors.AlreadyMember;
        }

        await database.Memberships.InsertOneAsync(membershipEntity, cancellationToken: cancellationToken);
        return await roster.ToSummaryAsync(membershipEntity);
    }

    public async Task<Result<Summary>> UpdateRoleAsync(
        string id,
        UpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var membershipId))
            return MembershipErrors.InvalidId;
        if (!Roster.TryNormalizeRole(request.Role, out var normalizedRole))
            return MembershipErrors.InvalidRole(Roster.RoleOptions());
        var membership = await FindAsync(membershipId, organizationId, cancellationToken);
        if (membership is null)
            return MembershipErrors.NotFound(id);
        if (membership.Role == nameof(RoleType.Owner) && normalizedRole != nameof(RoleType.Owner) &&
            !await roster.HasOtherActiveOwnerAsync(organizationId, membershipId))
            return MembershipErrors.CannotDemoteLastOwner;
        membership.Role = normalizedRole;
        membership.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Memberships.ReplaceOneAsync(
            existing => existing.Id == membership.Id, membership, cancellationToken: cancellationToken);
        return await roster.ToSummaryAsync(membership);
    }

    public async Task<Result> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!currentTenant.TryGetIdentity(out _, out var organizationId))
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var membershipId))
            return MembershipErrors.InvalidId;
        var membership = await FindAsync(membershipId, organizationId, cancellationToken);
        if (membership is null)
            return MembershipErrors.NotFound(id);
        if (membership.Role == nameof(RoleType.Owner) && currentTenant.Role != nameof(RoleType.Owner))
            return MembershipErrors.OnlyOwnerCanRemoveOwner;
        if (membership.Role == nameof(RoleType.Owner) && membership.Status == nameof(RoleStatus.Active) &&
            !await roster.HasOtherActiveOwnerAsync(organizationId, membershipId))
            return MembershipErrors.CannotRemoveLastOwner;
        await database.Memberships.DeleteOneAsync(existing => existing.Id == membership.Id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Summary>> AcceptAsync(string id, CancellationToken cancellationToken = default)
    {
        var callerUserId = currentTenant.UserId;
        if (callerUserId == ObjectId.Empty)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var membershipId))
            return MembershipErrors.InvalidId;
        var membership = await database.Memberships
            .Find(existing => existing.Id == membershipId)
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null || membership.Status != nameof(RoleStatus.Invited))
            return MembershipErrors.InviteNotFound;
        if (membership.UserId != ObjectId.Empty && membership.UserId != callerUserId)
            return MembershipErrors.InviteBelongsToAnotherAccount;
        if (membership.UserId == ObjectId.Empty)
        {
            var ownsInvitedEmail = await database.AuthenticationIdentities
                .Find(identity => identity.UserId == callerUserId && identity.Email == membership.Email)
                .AnyAsync(cancellationToken);
            if (!ownsInvitedEmail)
                return MembershipErrors.InviteBelongsToAnotherAccount;
        }

        membership.UserId = callerUserId;
        membership.Status = nameof(RoleStatus.Active);
        membership.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await database.Memberships.ReplaceOneAsync(
            existing => existing.Id == membership.Id, membership, cancellationToken: cancellationToken);
        return await roster.ToSummaryAsync(membership);
    }

    private Task<MembershipEntity?> FindAsync(
        ObjectId membershipId,
        ObjectId organizationId,
        CancellationToken cancellationToken)
    {
        return database.Memberships
            .Find<MembershipEntity>(membership =>
                membership.Id == membershipId && membership.OrganizationId == organizationId)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    private static Result<InviteDetails> ValidateInvite(InviteRequest request)
    {
        var errors = new List<(string Field, string Message)>();
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            errors.Add((MembershipErrors.EmailField, MembershipErrors.EmailRequired));
        var hasValidRole = Roster.TryNormalizeRole(request.Role, out var normalizedRole);
        if (!hasValidRole)
            errors.Add((MembershipErrors.RoleField, MembershipErrors.InvalidRole(Roster.RoleOptions()).Description));
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new InviteDetails(email, normalizedRole);
    }

    private sealed record InviteDetails(string Email, string Role);
}