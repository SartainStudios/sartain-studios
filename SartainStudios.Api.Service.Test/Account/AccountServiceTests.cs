using MongoDB.Bson;
using SartainStudios.Api.Service.Account;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Service.Test.Account;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsNotResolvedWhenTenantIsAnonymous()
    {
        var fixture = CreateFixture(TestTenant.Anonymous());

        var result = await fixture.Service.GetAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotResolvedWhenUserNotFound()
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var fixture = CreateFixture(TestTenant.Create(userId, organizationId));

        var result = await fixture.Service.GetAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotResolvedWhenNoActiveMembership()
    {
        var user = new UserProfile { DisplayName = "Test User" };
        var organizationId = ObjectId.GenerateNewId();
        var fixture = CreateFixture(TestTenant.Create(user.Id, organizationId));
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.Memberships.Seed(new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Suspended),
            Email = "user@example.com"
        });

        var result = await fixture.Service.GetAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsAccountResponseWhenContextResolved()
    {
        var (fixture, user, membership) = CreateSeededFixture();

        var result = await fixture.Service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id.ToString(), result.Value.User.Id);
        Assert.Equal(user.DisplayName, result.Value.User.DisplayName);
        Assert.Equal(membership.Email, result.Value.User.Email);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsValidationErrorWhenDisplayNameIsEmpty()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateProfileAsync(new UpdateProfileRequest("   ", null));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.DisplayNameRequired, validation.Errors[AccountErrors.DisplayNameField]);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsValidationErrorWhenDisplayNameTooLong()
    {
        var (fixture, _, _) = CreateSeededFixture();
        var longName = new string('a', AccountErrors.DisplayNameMaximumLength + 1);

        var result = await fixture.Service.UpdateProfileAsync(new UpdateProfileRequest(longName, null));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.DisplayNameTooLong, validation.Errors[AccountErrors.DisplayNameField]);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsValidationErrorWhenProfilePhotoUrlIsNotHttpUrl()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateProfileAsync(
            new UpdateProfileRequest("Valid Name", "ftp://invalid.url/photo.png"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.ProfilePhotoUrlInvalid, validation.Errors[AccountErrors.ProfilePhotoUrlField]);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsValidationErrorWhenProfilePhotoUrlTooLong()
    {
        var (fixture, _, _) = CreateSeededFixture();
        var longUrl = "https://" + new string('a', AccountErrors.ProfilePhotoUrlMaximumLength) + ".com";

        var result = await fixture.Service.UpdateProfileAsync(new UpdateProfileRequest("Valid Name", longUrl));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.ProfilePhotoUrlTooLong, validation.Errors[AccountErrors.ProfilePhotoUrlField]);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesProfileSuccessfully()
    {
        var (fixture, user, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateProfileAsync(
            new UpdateProfileRequest("  Updated Name  ", "https://photo.example.com/img.png"));

        Assert.True(result.IsSuccess);
        var updated = fixture.Harness.UserProfiles.Documents.Single(u => u.Id == user.Id);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("https://photo.example.com/img.png", updated.ProfilePhotoUrl);
        Assert.Equal(fixture.Now.UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateNotificationPreferencesAsync_ReturnsValidationErrorForWeeklyLimitOutOfRange()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateNotificationPreferencesAsync(
            new NotificationPreferencesRequest(0, 0));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.WeeklyHourLimitOutOfRange,
            validation.Errors[AccountErrors.WeeklyHourLimitMinutesField]);
    }

    [Fact]
    public async Task UpdateNotificationPreferencesAsync_ReturnsValidationErrorForNegativeHourLimitWarning()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateNotificationPreferencesAsync(
            new NotificationPreferencesRequest(null, -1));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.HourLimitWarningNegative,
            validation.Errors[AccountErrors.HourLimitWarningMinutesField]);
    }

    [Fact]
    public async Task UpdateNotificationPreferencesAsync_ReturnsValidationErrorWhenWarningExceedsLimit()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UpdateNotificationPreferencesAsync(
            new NotificationPreferencesRequest(60, 90));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.HourLimitWarningExceedsLimit,
            validation.Errors[AccountErrors.HourLimitWarningMinutesField]);
    }

    [Fact]
    public async Task UpdateNotificationPreferencesAsync_UpdatesSuccessfully()
    {
        var (fixture, _, membership) = CreateSeededFixture();

        var result = await fixture.Service.UpdateNotificationPreferencesAsync(
            new NotificationPreferencesRequest(480, 30));

        Assert.True(result.IsSuccess);
        var updated = fixture.Harness.Memberships.Documents.Single(m => m.Id == membership.Id);
        Assert.Equal(480, updated.WeeklyHourLimitMinutes);
        Assert.Equal(30, updated.HourLimitWarningMinutes);
        Assert.Equal(fixture.Now.UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsValidationErrorWhenNewPasswordTooShort()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.ChangePasswordAsync(new ChangePasswordRequest(null, "abc"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.NewPasswordTooShort(Password.MinimumLength),
            validation.Errors[AccountErrors.NewPasswordField]);
    }

    [Fact]
    public async Task ChangePasswordAsync_RequiresCurrentPasswordWhenCredentialExists()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash("ExistingPass1!")
        });

        var result = await fixture.Service.ChangePasswordAsync(new ChangePasswordRequest(null, "NewPassword1!"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.CurrentPasswordRequired,
            validation.Errors[AccountErrors.CurrentPasswordField]);
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsCurrentPasswordIncorrectWhenNotMatching()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash("ExistingPass1!")
        });

        var result = await fixture.Service.ChangePasswordAsync(
            new ChangePasswordRequest("WrongPassword!", "NewPassword1!"));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.CurrentPasswordIncorrect,
            validation.Errors[AccountErrors.CurrentPasswordField]);
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsNewPasswordMatchesCurrentWhenSamePassword()
    {
        var (fixture, user, _) = CreateSeededFixture();
        const string existingPassword = "ExistingPass1!";
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash(existingPassword)
        });

        var result = await fixture.Service.ChangePasswordAsync(
            new ChangePasswordRequest(existingPassword, existingPassword));

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.NewPasswordMatchesCurrent,
            validation.Errors[AccountErrors.NewPasswordField]);
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangesPasswordSuccessfully()
    {
        var (fixture, user, _) = CreateSeededFixture();
        const string oldPassword = "OldPassword1!";
        const string newPassword = "NewPassword1!";
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash(oldPassword)
        });

        var result = await fixture.Service.ChangePasswordAsync(new ChangePasswordRequest(oldPassword, newPassword));

        Assert.True(result.IsSuccess);
        var credential = Assert.Single(fixture.Harness.EmailPasswordCredentials.Documents);
        Assert.True(Password.Verify(newPassword, credential.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_AddsPasswordAndEmailIdentityWhenNoCredentialExists()
    {
        var (fixture, _, _) = CreateSeededFixture("newpass@example.com");

        var result = await fixture.Service.ChangePasswordAsync(new ChangePasswordRequest(null, "NewPassword1!"));

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.Harness.EmailPasswordCredentials.Documents);
        Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsEmailAlreadyUsedWhenEmailIdentityExists()
    {
        const string email = "taken@example.com";
        var (fixture, _, _) = CreateSeededFixture(email);
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = ObjectId.GenerateNewId(),
            Provider = IdentityProvider.Email,
            ProviderSubject = email,
            Email = email,
            EmailVerified = true
        });

        var result = await fixture.Service.ChangePasswordAsync(new ChangePasswordRequest(null, "NewPassword1!"));

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.EmailAlreadyUsed, result.Error);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_ReturnsValidationErrorForUndefinedProvider()
    {
        var (fixture, _, _) = CreateSeededFixture();

        var result = await fixture.Service.UnlinkIdentityAsync((IdentityProvider)99);

        Assert.True(result.IsFailure);
        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(AccountErrors.ProviderUnknown, validation.Errors[AccountErrors.ProviderField]);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_ReturnsIdentityNotLinkedWhenProviderNotFound()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Google,
            ProviderSubject = "sub-1",
            Email = "user@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.UnlinkIdentityAsync(IdentityProvider.Email);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.IdentityNotLinked, result.Error);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_ReturnsLastSignInMethodWhenOnlyOneIdentityLinked()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.AuthenticationIdentities.Seed(new AuthenticationIdentity
        {
            UserId = user.Id,
            Provider = IdentityProvider.Google,
            ProviderSubject = "sub-1",
            Email = "user@example.com",
            EmailVerified = true
        });

        var result = await fixture.Service.UnlinkIdentityAsync(IdentityProvider.Google);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.LastSignInMethod, result.Error);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_RemovesTargetIdentitySuccessfully()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.AuthenticationIdentities.Seed(
            new AuthenticationIdentity
            {
                UserId = user.Id,
                Provider = IdentityProvider.Google,
                ProviderSubject = "sub-1",
                Email = "user@example.com",
                EmailVerified = true
            },
            new AuthenticationIdentity
            {
                UserId = user.Id,
                Provider = IdentityProvider.Email,
                ProviderSubject = "user@example.com",
                Email = "user@example.com",
                EmailVerified = true
            });

        var result = await fixture.Service.UnlinkIdentityAsync(IdentityProvider.Google);

        Assert.True(result.IsSuccess);
        var remaining = Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
        Assert.Equal(IdentityProvider.Email, remaining.Provider);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_DeletesEmailPasswordCredentialWhenEmailProviderUnlinked()
    {
        var (fixture, user, _) = CreateSeededFixture();
        fixture.Harness.AuthenticationIdentities.Seed(
            new AuthenticationIdentity
            {
                UserId = user.Id,
                Provider = IdentityProvider.Google,
                ProviderSubject = "sub-1",
                Email = "user@example.com",
                EmailVerified = true
            },
            new AuthenticationIdentity
            {
                UserId = user.Id,
                Provider = IdentityProvider.Email,
                ProviderSubject = "user@example.com",
                Email = "user@example.com",
                EmailVerified = true
            });
        fixture.Harness.EmailPasswordCredentials.Seed(new EmailPasswordCredential
        {
            UserId = user.Id,
            PasswordHash = Password.Hash("Password1!")
        });

        var result = await fixture.Service.UnlinkIdentityAsync(IdentityProvider.Email);

        Assert.True(result.IsSuccess);
        var remaining = Assert.Single(fixture.Harness.AuthenticationIdentities.Documents);
        Assert.Equal(IdentityProvider.Google, remaining.Provider);
        Assert.Empty(fixture.Harness.EmailPasswordCredentials.Documents);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotResolvedWhenTenantIsAnonymous()
    {
        var fixture = CreateFixture(TestTenant.Anonymous());

        var result = await fixture.Service.DeleteAsync("America/Chicago", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotResolvedWhenUserNotFound()
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        var fixture = CreateFixture(TestTenant.Create(userId, organizationId));

        var result = await fixture.Service.DeleteAsync("America/Chicago", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_DeletesUserSuccessfully()
    {
        var user = new UserProfile { DisplayName = "To Delete" };
        var organizationId = ObjectId.GenerateNewId();
        var fixture = CreateFixture(TestTenant.Create(user.Id, organizationId));
        fixture.Harness.UserProfiles.Seed(user);

        var result = await fixture.Service.DeleteAsync("America/Chicago", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Harness.UserProfiles.Documents);
    }

    private static Fixture CreateFixture(CurrentTenant? currentTenant = null)
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc));
        var timeProvider = new StaticTimeProvider(now);
        var password = new Password(harness.Database);
        var draftInvoice = new Draft(harness.Database);
        var deletion = new Deletion(harness.Database, harness.Client, draftInvoice);
        var tenant = currentTenant ?? TestTenant.Anonymous();
        var service = new AccountService(harness.Database, tenant, password, deletion, timeProvider);
        return new Fixture(service, harness, now);
    }

    private static (Fixture Fixture, UserProfile User, SartainStudios.Schema.DatabaseEntity.Membership Membership)
        CreateSeededFixture(
            string email = "user@example.com")
    {
        var user = new UserProfile { DisplayName = "Test User" };
        var organizationId = ObjectId.GenerateNewId();
        var membership = new SartainStudios.Schema.DatabaseEntity.Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = nameof(RoleType.Owner),
            Status = nameof(RoleStatus.Active),
            Email = email
        };
        var fixture = CreateFixture(TestTenant.Create(user.Id, organizationId));
        fixture.Harness.UserProfiles.Seed(user);
        fixture.Harness.Memberships.Seed(membership);
        return (fixture, user, membership);
    }

    private sealed record Fixture(AccountService Service, MongoHarness Harness, DateTimeOffset Now);
}