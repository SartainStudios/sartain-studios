using MongoDB.Bson;
using SartainStudios.Api.Schema.Account;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Account;

public sealed class DeletionTests
{
    [Fact]
    public async Task DeleteUserAsync_ReturnsUserNotFoundWhenUserDoesNotExist()
    {
        var harness = new MongoHarness();
        var service = new Deletion(harness.Database, harness.Client, new Draft(harness.Database));

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

        var outcome = await service.DeleteUserAsync(ObjectId.GenerateNewId(), timeZone);

        Assert.Equal(DeletionOutcome.UserNotFound, outcome);
    }

    [Fact]
    public async Task DeleteUserAsync_DeletesOrganizationAndUserWhenSoleOwner()
    {
        var harness = new MongoHarness();
        var service = new Deletion(harness.Database, harness.Client, new Draft(harness.Database));
        var user = new UserProfile { DisplayName = "Owner" };
        var organization = new OrganizationEntity { Name = "Sole Org" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "owner@example.com"
        };
        harness.UserProfiles.Seed(user);
        harness.Organizations.Seed(organization);
        harness.Memberships.Seed(membership);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var outcome = await service.DeleteUserAsync(user.Id, timeZone);

        Assert.Equal(DeletionOutcome.Deleted, outcome);
        Assert.Empty(harness.Organizations.Documents);
        Assert.Empty(harness.Memberships.Documents);
        Assert.Empty(harness.UserProfiles.Documents);
        Assert.Equal(1, harness.CommittedTransactionCount);
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesMembershipWhenAnotherOwnerExists()
    {
        var harness = new MongoHarness();
        var service = new Deletion(harness.Database, harness.Client, new Draft(harness.Database));
        var user = new UserProfile { DisplayName = "Co-Owner" };
        var otherOwner = new UserProfile { DisplayName = "Other Owner" };
        var organization = new OrganizationEntity { Name = "Shared Org" };
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "coowner@example.com"
        };
        var otherMembership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = otherOwner.Id,
            OrganizationId = organization.Id,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = "otherowner@example.com"
        };
        harness.UserProfiles.Seed(user, otherOwner);
        harness.Organizations.Seed(organization);
        harness.Memberships.Seed(membership, otherMembership);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var outcome = await service.DeleteUserAsync(user.Id, timeZone);

        Assert.Equal(DeletionOutcome.Deleted, outcome);
        Assert.Single(harness.Organizations.Documents);
        var remaining = Assert.Single(harness.Memberships.Documents);
        Assert.Equal(otherMembership.Id, remaining.Id);
        Assert.Single(harness.UserProfiles.Documents);
        Assert.Equal(1, harness.CommittedTransactionCount);
    }

    [Fact]
    public async Task DeleteUserAsync_DeletesAllAuthenticationDataForUser()
    {
        var harness = new MongoHarness();
        var service = new Deletion(harness.Database, harness.Client, new Draft(harness.Database));
        var user = new UserProfile { DisplayName = "Auth User" };
        harness.UserProfiles.Seed(user);
        harness.AuthenticationSessions.Seed(new AuthenticationSession { UserId = user.Id });
        harness.AuthenticationIdentities.Seed(new AuthenticationIdentity { UserId = user.Id });
        harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential { UserId = user.Id });
        harness.PasswordResetTokens.Seed(new PasswordResetToken { UserId = user.Id });

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var outcome = await service.DeleteUserAsync(user.Id, timeZone);

        Assert.Equal(DeletionOutcome.Deleted, outcome);
        Assert.Empty(harness.AuthenticationSessions.Documents);
        Assert.Empty(harness.AuthenticationIdentities.Documents);
        Assert.Empty(harness.EmailPasswordCredentials.Documents);
        Assert.Empty(harness.PasswordResetTokens.Documents);
        Assert.Empty(harness.UserProfiles.Documents);
    }

    [Fact]
    public async Task DeleteUserAsync_AbortsTransactionAndReturnsConflictOnWriteFailure()
    {
        var harness = new MongoHarness();
        var service = new Deletion(harness.Database, harness.Client, new Draft(harness.Database));
        var user = new UserProfile { DisplayName = "Conflict User" };
        harness.UserProfiles.Seed(user);
        harness.AuthenticationSessions.WriteFailure = new InvalidOperationException("simulated failure");

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var outcome = await service.DeleteUserAsync(user.Id, timeZone);

        Assert.Equal(DeletionOutcome.Conflict, outcome);
        Assert.Equal(0, harness.CommittedTransactionCount);
        Assert.Equal(1, harness.AbortedTransactionCount);
    }
}