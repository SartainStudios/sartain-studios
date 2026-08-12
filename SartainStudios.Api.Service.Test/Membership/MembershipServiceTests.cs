using MongoDB.Bson;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Membership;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.Membership;
using AuthenticationIdentityEntity = SartainStudios.Schema.DatabaseEntity.AuthenticationIdentity;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;

namespace SartainStudios.Api.Service.Test.Membership;

public sealed class MembershipServiceTests
{
    private static MembershipService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        var roster = new Roster(harness.Database);
        return new MembershipService(
            harness.Database,
            tenant ?? TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()),
            roster,
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ListAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness, TestTenant.Anonymous());

        var result = await service.ListAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task InviteAsync_InsertsInvitedMembershipWhenValidRequest()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));

        var result = await service.InviteAsync(new InviteRequest(" invite@example.com ", "member"));

        Assert.True(result.IsSuccess);
        var membership = Assert.Single(harness.Memberships.Documents);
        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal("invite@example.com", membership.Email);
        Assert.Equal(nameof(RoleType.Member), membership.Role);
        Assert.Equal(nameof(RoleStatus.Invited), membership.Status);
        Assert.Equal(now.UtcDateTime, membership.UpdatedAt);
    }

    [Fact]
    public async Task UpdateRoleAsync_UpdatesMembershipRoleWhenFound()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));
        var membership = new MembershipEntity
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            Email = "member@example.com",
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Active)
        };
        harness.Memberships.Seed(membership);

        var result = await service.UpdateRoleAsync(membership.Id.ToString(), new UpdateRequest("administrator"));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RoleType.Administrator), harness.Memberships.Documents[0].Role);
        Assert.Equal(now.UtcDateTime, harness.Memberships.Documents[0].UpdatedAt);
    }

    [Fact]
    public async Task RemoveAsync_DeletesMembershipWhenFound()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var membership = new MembershipEntity
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            Email = "member@example.com",
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Active)
        };
        harness.Memberships.Seed(membership);

        var result = await service.RemoveAsync(membership.Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.Memberships.Documents);
    }

    [Fact]
    public async Task AcceptAsync_ActivatesInviteWhenCallerOwnsEmailIdentity()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var userId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(userId, ObjectId.GenerateNewId());
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));
        var invite = new MembershipEntity
        {
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.Empty,
            Email = "invite@example.com",
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited)
        };
        harness.Memberships.Seed(invite);
        harness.AuthenticationIdentities.Seed(new AuthenticationIdentityEntity
        {
            UserId = userId,
            Provider = IdentityProvider.Email,
            ProviderSubject = "invite@example.com",
            Email = "invite@example.com",
            EmailVerified = true
        });

        var result = await service.AcceptAsync(invite.Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, harness.Memberships.Documents[0].UserId);
        Assert.Equal(nameof(RoleStatus.Active), harness.Memberships.Documents[0].Status);
        Assert.Equal(now.UtcDateTime, harness.Memberships.Documents[0].UpdatedAt);
    }
}