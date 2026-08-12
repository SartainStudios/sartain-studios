using MongoDB.Bson;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Api.Service.Test.Account;

public sealed class RosterTests
{
    [Fact]
    public void TryNormalizeRole_ReturnsTrueForValidRole()
    {
        var result = Roster.TryNormalizeRole("Owner", out var normalized);

        Assert.True(result);
        Assert.Equal("Owner", normalized);
    }

    [Fact]
    public void TryNormalizeRole_ReturnsTrueForCaseInsensitiveMatch()
    {
        var result = Roster.TryNormalizeRole("administrator", out var normalized);

        Assert.True(result);
        Assert.Equal("Administrator", normalized);
    }

    [Fact]
    public void TryNormalizeRole_ReturnsFalseForInvalidRole()
    {
        var result = Roster.TryNormalizeRole("SuperAdmin", out var normalized);

        Assert.False(result);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalizeRole_ReturnsFalseForNullOrWhitespace()
    {
        Assert.False(Roster.TryNormalizeRole(null, out _));
        Assert.False(Roster.TryNormalizeRole("   ", out _));
    }

    [Fact]
    public void RoleOptions_ReturnsAllRoleNames()
    {
        var options = Roster.RoleOptions();

        Assert.Contains("Owner", options);
        Assert.Contains("Administrator", options);
        Assert.Contains("Member", options);
    }

    [Fact]
    public async Task HasOtherActiveOwnerAsync_ReturnsFalseWhenNoOtherOwnerExists()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();

        var result = await roster.HasOtherActiveOwnerAsync(organizationId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasOtherActiveOwnerAsync_ReturnsTrueWhenOtherOwnerExists()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();
        harness.Memberships.Seed(new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "owner@example.com"
        });

        var result = await roster.HasOtherActiveOwnerAsync(organizationId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasOtherActiveOwnerAsync_ReturnsFalseWhenOnlyOwnerIsExcludedByMembershipId()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();
        var owner = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "owner@example.com"
        };
        harness.Memberships.Seed(owner);

        var result = await roster.HasOtherActiveOwnerAsync(organizationId, owner.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task HasOtherActiveOwnerAsync_ReturnsFalseWhenOnlyOwnerIsExcludedByUserId()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.Memberships.Seed(new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "owner@example.com"
        });

        var result = await roster.HasOtherActiveOwnerAsync(organizationId, excludingUserId: userId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasOtherActiveOwnerAsync_IgnoresSuspendedOwners()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();
        harness.Memberships.Seed(new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Suspended),
            Email = "suspended@example.com"
        });

        var result = await roster.HasOtherActiveOwnerAsync(organizationId);

        Assert.False(result);
    }

    [Fact]
    public async Task ToSummaryAsync_ReturnsCorrectSummaryWithDisplayNameForLinkedUser()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var user = new UserProfile { DisplayName = "Alice" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Administrator),
            Status = nameof(RoleStatus.Active),
            Email = "alice@example.com"
        };
        harness.UserProfiles.Seed(user);

        var summary = await roster.ToSummaryAsync(membership);

        Assert.Equal(membership.Id.ToString(), summary.Id);
        Assert.Equal(membership.OrganizationId.ToString(), summary.OrganizationId);
        Assert.Equal(user.Id.ToString(), summary.UserId);
        Assert.Equal("Alice", summary.DisplayName);
        Assert.Equal("alice@example.com", summary.Email);
        Assert.Equal(nameof(RoleType.Administrator), summary.Role);
        Assert.Equal(nameof(RoleStatus.Active), summary.Status);
    }

    [Fact]
    public async Task ToSummaryAsync_ReturnsNullUserIdAndDisplayNameForUnlinkedMember()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited),
            Email = "invite@example.com"
        };

        var summary = await roster.ToSummaryAsync(membership);

        Assert.Null(summary.UserId);
        Assert.Null(summary.DisplayName);
        Assert.Equal("invite@example.com", summary.Email);
        Assert.Equal(nameof(RoleStatus.Invited), summary.Status);
    }

    [Fact]
    public async Task ToSummariesAsync_OrdersActiveMembersBeforeInvitedThenByEmail()
    {
        var harness = new MongoHarness();
        var roster = new Roster(harness.Database);
        var organizationId = ObjectId.GenerateNewId();
        var userA = new UserProfile { DisplayName = "User A" };
        var userZ = new UserProfile { DisplayName = "User Z" };
        var activeA = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = userA.Id,
            OrganizationId = organizationId,
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Active),
            Email = "a@example.com"
        };
        var activeZ = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = userZ.Id,
            OrganizationId = organizationId,
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Active),
            Email = "z@example.com"
        };
        var invited = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = organizationId,
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited),
            Email = "b@example.com"
        };
        harness.UserProfiles.Seed(userA, userZ);

        var summaries = await roster.ToSummariesAsync([activeA, activeZ, invited]);

        Assert.Equal(3, summaries.Count);
        Assert.Equal("a@example.com", summaries[0].Email);
        Assert.Equal("z@example.com", summaries[1].Email);
        Assert.Equal("b@example.com", summaries[2].Email);
        Assert.Equal(nameof(RoleStatus.Invited), summaries[2].Status);
    }
}