using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Organization;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.Organization;
using AuthenticationSessionEntity = SartainStudios.Schema.DatabaseEntity.AuthenticationSession;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using OrganizationCreateRequest = SartainStudios.Schema.Organization.CreateRequest;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using OrganizationUpdateRequest = SartainStudios.Schema.Organization.UpdateRequest;
using UserProfileEntity = SartainStudios.Schema.DatabaseEntity.UserProfile;

namespace SartainStudios.Api.Service.Test.Organization;

public sealed class OrganizationServiceTests
{
    private static OrganizationService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedTimeProvider = timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow);
        return new OrganizationService(
            harness.Database,
            tenant ?? TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()),
            new Lookup(harness.Database),
            new Provisioning(harness.Database),
            new Session(harness.Database, CreateToken(), resolvedTimeProvider),
            resolvedTimeProvider);
    }

    private static Token CreateToken()
    {
        return new Token(new Jwt
        {
            Issuer = "tests",
            Audience = "tests",
            SigningKey = "0123456789abcdef0123456789abcdef",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 7
        });
    }

    private static CurrentTenant CreateTenant(
        ObjectId userId,
        ObjectId organizationId,
        ObjectId? sessionId = null,
        string? email = null,
        string? displayName = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(nameof(JwtClaimName.OrganizationId), organizationId.ToString())
        };

        if (sessionId is { } resolvedSessionId)
            claims.Add(new Claim(nameof(JwtClaimName.SessionId), resolvedSessionId.ToString()));

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));

        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, displayName));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new CurrentTenant(accessor);
    }

    [Fact]
    public async Task ListMineAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness, TestTenant.Anonymous());

        var result = await service.ListMineAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task ListMineAsync_ReturnsActiveOrganizationFirstAndSkipsSuspendedMemberships()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var activeOrganizationId = ObjectId.GenerateNewId();
        var secondOrganizationId = ObjectId.GenerateNewId();
        var suspendedOrganizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(userId, activeOrganizationId);
        var service = CreateService(harness, tenant);

        harness.Memberships.Seed(
            new MembershipEntity
            {
                UserId = userId,
                OrganizationId = secondOrganizationId,
                Email = "member@example.com",
                Role = nameof(RoleType.Member),
                Status = nameof(RoleStatus.Active)
            },
            new MembershipEntity
            {
                UserId = userId,
                OrganizationId = activeOrganizationId,
                Email = "owner@example.com",
                Role = nameof(RoleType.Owner),
                Status = nameof(RoleStatus.Active)
            },
            new MembershipEntity
            {
                UserId = userId,
                OrganizationId = suspendedOrganizationId,
                Email = "suspended@example.com",
                Role = nameof(RoleType.Member),
                Status = nameof(RoleStatus.Suspended)
            });

        harness.Organizations.Seed(
            new OrganizationEntity
            {
                Id = secondOrganizationId,
                Name = "Alpha Company",
                Email = "alpha@example.com"
            },
            new OrganizationEntity
            {
                Id = activeOrganizationId,
                Name = "Zulu Company",
                Email = "zulu@example.com"
            },
            new OrganizationEntity
            {
                Id = suspendedOrganizationId,
                Name = "Suspended Company",
                Email = "suspended@example.com"
            });

        var result = await service.ListMineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(activeOrganizationId.ToString(), result.Value[0].Id);
        Assert.True(result.Value[0].IsActive);
        Assert.Equal(secondOrganizationId.ToString(), result.Value[1].Id);
    }

    [Fact]
    public async Task GetAsync_ReturnsForbiddenWhenActiveMembershipDoesNotExist()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var activeOrganizationId = ObjectId.GenerateNewId();
        var targetOrganizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(userId, activeOrganizationId);
        var service = CreateService(harness, tenant);

        harness.Memberships.Seed(new MembershipEntity
        {
            UserId = userId,
            OrganizationId = targetOrganizationId,
            Email = "member@example.com",
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Invited)
        });

        var result = await service.GetAsync(targetOrganizationId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task CreateAsync_CreatesOrganizationAndOwnerMembershipWhenRequestIsValid()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(userId, ObjectId.GenerateNewId());
        var service = CreateService(harness, tenant);

        harness.UserProfiles.Seed(new UserProfileEntity
        {
            Id = userId,
            DisplayName = "Taylor"
        });

        var result = await service.CreateAsync(new OrganizationCreateRequest(
            "  New Org  ",
            new Address { Line1 = " 100 Main St " },
            "  team@example.com  ",
            " +15551234567 "));

        Assert.True(result.IsSuccess);
        var organization = Assert.Single(harness.Organizations.Documents);
        Assert.Equal("New Org", organization.Name);
        Assert.Equal("team@example.com", organization.Email);
        Assert.Equal("+15551234567", organization.PhoneNumber);
        var membership = Assert.Single(harness.Memberships.Documents);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(nameof(RoleType.Owner), membership.Role);
        Assert.Equal(nameof(RoleStatus.Active), membership.Status);
        Assert.Equal(organization.Id, membership.OrganizationId);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOrganizationWhenCallerTargetsActiveOrganization()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(userId, organizationId);
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));

        harness.Organizations.Seed(new OrganizationEntity
        {
            Id = organizationId,
            Name = "Old Name",
            Email = "old@example.com",
            PhoneNumber = "1111111111"
        });

        harness.Memberships.Seed(new MembershipEntity
        {
            UserId = userId,
            OrganizationId = organizationId,
            Email = "member@example.com",
            Role = nameof(RoleType.Administrator),
            Status = nameof(RoleStatus.Active)
        });

        var result = await service.UpdateAsync(
            organizationId.ToString(),
            new OrganizationUpdateRequest(
                "  Updated Name  ",
                new Address { Line1 = " 200 Main St " },
                "updated@example.com",
                "+15550001111"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", harness.Organizations.Documents[0].Name);
        Assert.Equal("200 Main St", harness.Organizations.Documents[0].Address.Line1);
        Assert.Equal("updated@example.com", harness.Organizations.Documents[0].Email);
        Assert.Equal(now.UtcDateTime, harness.Organizations.Documents[0].UpdatedAt);
        Assert.Equal(nameof(RoleType.Administrator), result.Value.Role);
    }

    [Fact]
    public async Task SwitchAsync_RevokesCurrentSessionAndIssuesSessionForTargetOrganization()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        var currentOrganizationId = ObjectId.GenerateNewId();
        var targetOrganizationId = ObjectId.GenerateNewId();
        var currentSessionId = ObjectId.GenerateNewId();
        var tenant = CreateTenant(userId, currentOrganizationId, currentSessionId, "member@example.com", "Taylor");
        var service = CreateService(harness, tenant);

        harness.AuthenticationSessions.Seed(new AuthenticationSessionEntity
        {
            Id = currentSessionId,
            UserId = userId,
            OrganizationId = currentOrganizationId,
            Provider = IdentityProvider.Email,
            RefreshTokenHash = "existing",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        harness.UserProfiles.Seed(new UserProfileEntity
        {
            Id = userId,
            DisplayName = "Taylor"
        });

        harness.Memberships.Seed(new MembershipEntity
        {
            UserId = userId,
            OrganizationId = targetOrganizationId,
            Email = "member@example.com",
            Role = nameof(RoleType.Member),
            Status = nameof(RoleStatus.Active)
        });

        harness.Organizations.Seed(new OrganizationEntity
        {
            Id = targetOrganizationId,
            Name = "Target Org",
            Email = "target@example.com"
        });

        var result = await service.SwitchAsync(targetOrganizationId.ToString());

        Assert.True(result.IsSuccess);
        Assert.NotNull(harness.AuthenticationSessions.Documents.Single(x => x.Id == currentSessionId).RevokedAt);
        Assert.Equal(2, harness.AuthenticationSessions.Documents.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.Equal(targetOrganizationId.ToString(), result.Value.Organization.Id);
        Assert.Equal(nameof(RoleType.Member), result.Value.Organization.Role);
    }
}
