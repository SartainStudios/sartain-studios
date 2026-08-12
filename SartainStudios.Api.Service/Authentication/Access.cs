using MongoDB.Driver;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Membership;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;

namespace SartainStudios.Api.Service.Authentication;

public sealed class Access(Database database, CurrentTenant currentTenant)
{
    public async Task<TenantContext?> LoadContextAsync()
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var organizationId)) return null;
        var membershipExists = await database.Memberships
            .Find(x => x.UserId == userId && x.OrganizationId == organizationId &&
                       x.Status == nameof(RoleStatus.Active))
            .AnyAsync();
        return membershipExists ? new TenantContext(userId, organizationId) : null;
    }

    public async Task<MembershipEntity?> LoadMembershipAsync()
    {
        if (!currentTenant.TryGetIdentity(out var userId, out var organizationId)) return null;
        return await database.Memberships
            .Find(x => x.UserId == userId && x.OrganizationId == organizationId &&
                       x.Status == nameof(RoleStatus.Active))
            .FirstOrDefaultAsync();
    }
}