using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Account;

public sealed class Provisioning(Database database)
{
    public async Task<(SartainStudios.Schema.DatabaseEntity.Membership Membership, OrganizationEntity Organization)>
        CreateOrganizationAsync(
            UserProfile user,
            string? requestedName,
            Address? requestedAddress,
            string? requestedEmail,
            string? requestedPhoneNumber,
            string membershipEmail,
            DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;

        var organization = new OrganizationEntity
        {
            Name = BuildOrganizationName(user.DisplayName, requestedName),
            Address = requestedAddress?.Trimmed() ?? new Address(),
            Email = requestedEmail?.Trim() ?? membershipEmail,
            PhoneNumber = requestedPhoneNumber?.Trim() ?? string.Empty,
            UpdatedAt = now
        };

        await database.Organizations.InsertOneAsync(organization);

        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = membershipEmail,
            UpdatedAt = now
        };

        await database.Memberships.InsertOneAsync(membership);

        return (membership, organization);
    }

    public async Task LinkPendingInvitesAsync(UserProfile user, string email, DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;

        var pending = await database.Memberships
            .Find(m => m.Status == nameof(RoleStatus.Invited)
                       && m.Email == email
                       && m.UserId == ObjectId.Empty)
            .ToListAsync();

        foreach (var membership in pending)
        {
            membership.UserId = user.Id;
            membership.UpdatedAt = now;

            await database.Memberships.ReplaceOneAsync(m => m.Id == membership.Id, membership);
        }
    }

    private static string BuildOrganizationName(string displayName, string? requestedName)
    {
        return !string.IsNullOrWhiteSpace(requestedName) ? requestedName.Trim() : displayName.Trim();
    }
}