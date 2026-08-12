using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class AccessTests
{
    [Fact]
    public async Task LoadContextAsync_ReturnsNullWhenTenantCannotBeResolved()
    {
        var harness = new MongoHarness();
        var service = new Access(harness.Database, TestTenant.Anonymous());

        var context = await service.LoadContextAsync();

        Assert.Null(context);
    }

    [Fact]
    public async Task LoadContextAsync_ReturnsNullWhenMembershipIsNotActive()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        harness.Memberships.Seed(new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = userId,
            OrganizationId = organizationId,
            Status = nameof(RoleStatus.Suspended),
            Role = "Owner",
            Email = "member@example.com"
        });
        var service = new Access(harness.Database, TestTenant.Create(userId, organizationId));

        var context = await service.LoadContextAsync();

        Assert.Null(context);
    }

    [Fact]
    public async Task LoadMembershipAsync_ReturnsMembershipForActiveTenant()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = userId,
            OrganizationId = organizationId,
            Status = nameof(RoleStatus.Active),
            Role = "Administrator",
            Email = "member@example.com"
        };
        harness.Memberships.Seed(membership);
        var service = new Access(harness.Database, TestTenant.Create(userId, organizationId));

        var loaded = await service.LoadMembershipAsync();

        Assert.NotNull(loaded);
        Assert.Equal(membership.Id, loaded.Id);
    }
}