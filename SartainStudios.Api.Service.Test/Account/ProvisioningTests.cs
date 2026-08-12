using MongoDB.Bson;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Api.Service.Test.Account;

public sealed class ProvisioningTests
{
    [Fact]
    public async Task CreateOrganizationAsync_UsesDisplayNameWhenNoRequestedNameProvided()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "  Jane Doe  " };

        var (_, org) = await service.CreateOrganizationAsync(user, null, null, null, null, "jane@example.com");

        Assert.Equal("Jane Doe", org.Name);
    }

    [Fact]
    public async Task CreateOrganizationAsync_UsesRequestedNameWhenProvided()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "Jane Doe" };

        var (_, org) = await service.CreateOrganizationAsync(
            user, "  Acme Corp  ", null, null, null, "jane@example.com");

        Assert.Equal("Acme Corp", org.Name);
    }

    [Fact]
    public async Task CreateOrganizationAsync_InsertsOrganizationAndOwnerMembership()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "Jane Doe" };
        var timestamp = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        var (membership, org) = await service.CreateOrganizationAsync(
            user, "My Org", null, null, null, "jane@example.com", timestamp);

        Assert.Single(harness.Organizations.Documents);
        Assert.Single(harness.Memberships.Documents);
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(org.Id, membership.OrganizationId);
        Assert.Equal(nameof(RoleType.Owner), membership.Role);
        Assert.Equal(nameof(RoleStatus.Active), membership.Status);
        Assert.Equal("jane@example.com", membership.Email);
        Assert.Equal(timestamp, org.UpdatedAt);
        Assert.Equal(timestamp, membership.UpdatedAt);
    }

    [Fact]
    public async Task CreateOrganizationAsync_SetsOrganizationEmailFromRequestedEmailWhenProvided()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "Jane Doe" };

        var (_, org) = await service.CreateOrganizationAsync(
            user, "My Org", null, "billing@myorg.com", null, "jane@example.com");

        Assert.Equal("billing@myorg.com", org.Email);
    }

    [Fact]
    public async Task CreateOrganizationAsync_FallsBackToMembershipEmailWhenNoRequestedEmailProvided()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "Jane Doe" };

        var (_, org) = await service.CreateOrganizationAsync(
            user, "My Org", null, null, null, "jane@example.com");

        Assert.Equal("jane@example.com", org.Email);
    }

    [Fact]
    public async Task CreateOrganizationAsync_TrimsAddressWhenProvided()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "Jane Doe" };
        var address = new Address
        {
            Line1 = "  123 Main St  ",
            City = "  Springfield  ",
            Country = "  US  "
        };

        var (_, org) = await service.CreateOrganizationAsync(
            user, "My Org", address, null, null, "jane@example.com");

        Assert.Equal("123 Main St", org.Address.Line1);
        Assert.Equal("Springfield", org.Address.City);
        Assert.Equal("US", org.Address.Country);
    }

    [Fact]
    public async Task LinkPendingInvitesAsync_DoesNothingWhenNoPendingInvitesExist()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "New User" };

        await service.LinkPendingInvitesAsync(user, "newuser@example.com");

        Assert.Empty(harness.Memberships.Replaced);
    }

    [Fact]
    public async Task LinkPendingInvitesAsync_LinksPendingInvitesToUser()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "New User" };
        const string email = "invite@example.com";
        var timestamp = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        var pending = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited),
            Email = email
        };
        harness.Memberships.Seed(pending);

        await service.LinkPendingInvitesAsync(user, email, timestamp);

        var updated = Assert.Single(harness.Memberships.Documents);
        Assert.Equal(user.Id, updated.UserId);
        Assert.Equal(timestamp, updated.UpdatedAt);
    }

    [Fact]
    public async Task LinkPendingInvitesAsync_DoesNotLinkMembershipsWithDifferentEmail()
    {
        var harness = new MongoHarness();
        var service = new Provisioning(harness.Database);
        var user = new UserProfile { DisplayName = "New User" };
        var unrelatedPending = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            OrganizationId = ObjectId.GenerateNewId(),
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited),
            Email = "other@example.com"
        };
        harness.Memberships.Seed(unrelatedPending);

        await service.LinkPendingInvitesAsync(user, "newuser@example.com");

        var unchanged = Assert.Single(harness.Memberships.Documents);
        Assert.Equal(ObjectId.Empty, unchanged.UserId);
    }
}