using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Validation;
using SartainStudios.Schema.Membership;
using Summary = SartainStudios.Schema.Membership.Summary;

namespace SartainStudios.Api.Service.Account;

public sealed class Roster(Database database)
{
    public static bool TryNormalizeRole(string? role, out string normalizedRole)
    {
        return EnumName.TryNormalize<RoleType>(role, out normalizedRole);
    }

    public static string RoleOptions()
    {
        return EnumName.Options<RoleType>();
    }

    public async Task<bool> HasOtherActiveOwnerAsync(
        ObjectId organizationId,
        ObjectId? excludingMembershipId = null,
        ObjectId? excludingUserId = null,
        IClientSessionHandle? mongoSession = null)
    {
        var filter =
            Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Eq(m => m.OrganizationId, organizationId)
            & Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Eq(m => m.Role, nameof(RoleType.Owner))
            & Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Eq(m => m.Status,
                nameof(RoleStatus.Active));

        if (excludingMembershipId.HasValue)
            filter &= Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Ne(m => m.Id,
                excludingMembershipId.Value);

        if (excludingUserId.HasValue)
            filter &= Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Ne(m => m.UserId,
                excludingUserId.Value);

        return mongoSession is null
            ? await database.Memberships.Find(filter).AnyAsync()
            : await database.Memberships.Find(mongoSession, filter).AnyAsync();
    }

    public async Task<Summary> ToSummaryAsync(SartainStudios.Schema.DatabaseEntity.Membership membership)
    {
        var displayNames = await LoadDisplayNamesAsync([membership]);

        return ToSummary(membership, displayNames);
    }

    public async Task<IReadOnlyList<Summary>> ToSummariesAsync(
        IReadOnlyList<SartainStudios.Schema.DatabaseEntity.Membership> memberships)
    {
        var displayNames = await LoadDisplayNamesAsync(memberships);

        return memberships
            .Select(membership => ToSummary(membership, displayNames))
            .OrderBy(summary => summary.Status == nameof(RoleStatus.Invited) ? 1 : 0)
            .ThenBy(summary => summary.Email)
            .ToList();
    }

    private async Task<Dictionary<ObjectId, string>> LoadDisplayNamesAsync(
        IReadOnlyList<SartainStudios.Schema.DatabaseEntity.Membership> memberships)
    {
        var userIds = memberships
            .Where(m => m.UserId != ObjectId.Empty)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0) return [];

        var users = await database.UserProfiles
            .Find(u => userIds.Contains(u.Id))
            .ToListAsync();

        return users.ToDictionary(u => u.Id, u => u.DisplayName);
    }

    private static Summary ToSummary(SartainStudios.Schema.DatabaseEntity.Membership membership,
        IReadOnlyDictionary<ObjectId, string> displayNames)
    {
        var isLinked = membership.UserId != ObjectId.Empty;

        return new Summary(
            membership.Id.ToString(),
            membership.OrganizationId.ToString(),
            isLinked ? membership.UserId.ToString() : null,
            isLinked ? displayNames.GetValueOrDefault(membership.UserId) : null,
            membership.Email,
            membership.Role,
            membership.Status,
            membership.CreatedAt);
    }
}