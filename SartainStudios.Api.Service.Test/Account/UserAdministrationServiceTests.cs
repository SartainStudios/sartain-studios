using MongoDB.Bson;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Service.Test.Account;

public sealed class UserAdministrationServiceTests
{
    [Fact]
    public async Task IsAdministratorAsync_IsTrueOnlyForAdministrators()
    {
        var (admin, regular, _) = Seed(out var fixture);

        Assert.True(await CreateService(fixture, admin.Id).IsAdministratorAsync());
        Assert.False(await CreateService(fixture, regular.Id).IsAdministratorAsync());
        Assert.False(await CreateService(fixture, ObjectId.GenerateNewId()).IsAdministratorAsync());
    }

    [Fact]
    public async Task ListAsync_ReturnsNotFoundForNonAdministrator()
    {
        var (_, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, regular.Id).ListAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.AdministrationNotFound, result.Error);
    }

    [Fact]
    public async Task ListAsync_ReturnsNotFoundForAnonymous()
    {
        Seed(out var fixture);
        var service = new UserAdministrationService(
            fixture.Database,
            TestTenant.Anonymous(),
            new Deletion(fixture.Database, fixture.Client, new Draft(fixture.Database)),
            TimeProvider.System);

        var result = await service.ListAsync();

        Assert.Equal(AccountErrors.AdministrationNotFound, result.Error);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllUsersForAdministrator()
    {
        var (admin, _, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, user => user.Id == admin.Id.ToString() && user.IsAdministrator);
        Assert.Contains(result.Value, user => user.Emails.Contains("regular@example.com"));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundForNonAdministrator()
    {
        var (_, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, regular.Id).UpdateAsync(
            regular.Id.ToString(), new AdministratorUpdateUserRequest("Hacker", null, true));

        Assert.Equal(AccountErrors.AdministrationNotFound, result.Error);
        Assert.False(fixture.UserProfiles.Documents.Single(user => user.Id == regular.Id).IsAdministrator);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesUserForAdministrator()
    {
        var (admin, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).UpdateAsync(
            regular.Id.ToString(),
            new AdministratorUpdateUserRequest("  New Name  ", "https://photo.example.com/a.png", true));

        Assert.True(result.IsSuccess);
        var updated = fixture.UserProfiles.Documents.Single(user => user.Id == regular.Id);
        Assert.Equal("New Name", updated.DisplayName);
        Assert.Equal("https://photo.example.com/a.png", updated.ProfilePhotoUrl);
        Assert.True(updated.IsAdministrator);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsValidationErrorForBlankDisplayName()
    {
        var (admin, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).UpdateAsync(
            regular.Id.ToString(), new AdministratorUpdateUserRequest("  ", null, false));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUserNotFoundForUnknownUser()
    {
        var (admin, _, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).UpdateAsync(
            ObjectId.GenerateNewId().ToString(), new AdministratorUpdateUserRequest("Name", null, false));

        Assert.Equal(AccountErrors.UserNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_PreventsAdministratorFromRemovingOwnAccess()
    {
        var (admin, _, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).UpdateAsync(
            admin.Id.ToString(), new AdministratorUpdateUserRequest("Admin", null, false));

        Assert.Equal(AccountErrors.CannotModifySelf, result.Error);
        Assert.True(fixture.UserProfiles.Documents.Single(user => user.Id == admin.Id).IsAdministrator);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundForNonAdministrator()
    {
        var (admin, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, regular.Id).DeleteAsync(admin.Id.ToString(), null);

        Assert.Equal(AccountErrors.AdministrationNotFound, result.Error);
        Assert.Equal(2, fixture.UserProfiles.Documents.Count);
    }

    [Fact]
    public async Task DeleteAsync_DeletesUserForAdministrator()
    {
        var (admin, regular, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).DeleteAsync(regular.Id.ToString(), "America/Chicago");

        Assert.True(result.IsSuccess);
        var remaining = Assert.Single(fixture.UserProfiles.Documents);
        Assert.Equal(admin.Id, remaining.Id);
    }

    [Fact]
    public async Task DeleteAsync_PreventsAdministratorFromDeletingSelf()
    {
        var (admin, _, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).DeleteAsync(admin.Id.ToString(), null);

        Assert.Equal(AccountErrors.CannotModifySelf, result.Error);
        Assert.Equal(2, fixture.UserProfiles.Documents.Count);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsUserNotFoundForInvalidId()
    {
        var (admin, _, _) = Seed(out var fixture);

        var result = await CreateService(fixture, admin.Id).DeleteAsync("not-an-id", null);

        Assert.Equal(AccountErrors.UserNotFound, result.Error);
    }

    private static UserAdministrationService CreateService(MongoHarness harness, ObjectId currentUserId)
    {
        return new UserAdministrationService(
            harness.Database,
            TestTenant.Create(currentUserId),
            new Deletion(harness.Database, harness.Client, new Draft(harness.Database)),
            TimeProvider.System);
    }

    private static (UserProfile Admin, UserProfile Regular, MongoHarness Harness) Seed(out MongoHarness harness)
    {
        harness = new MongoHarness();
        var admin = new UserProfile { DisplayName = "Admin", IsAdministrator = true };
        var regular = new UserProfile { DisplayName = "Regular" };
        harness.UserProfiles.Seed(admin, regular);
        harness.Memberships.Seed(
            new SartainStudios.Schema.DatabaseEntity.Membership
            {
                UserId = admin.Id, OrganizationId = ObjectId.GenerateNewId(), Email = "admin@example.com"
            },
            new SartainStudios.Schema.DatabaseEntity.Membership
            {
                UserId = regular.Id, OrganizationId = ObjectId.GenerateNewId(), Email = "regular@example.com"
            });
        return (admin, regular, harness);
    }
}