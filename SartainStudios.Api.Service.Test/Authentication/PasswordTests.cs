using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Test.Authentication;

public sealed class PasswordTests
{
    [Fact]
    public void MeetsPolicy_ValidatesLengthAndWhitespace()
    {
        Assert.False(Password.MeetsPolicy(null));
        Assert.False(Password.MeetsPolicy("   "));
        Assert.False(Password.MeetsPolicy("short"));
        Assert.True(Password.MeetsPolicy(new string('a', Password.MinimumLength)));
    }

    [Fact]
    public void HashAndVerify_RoundTripSuccessfully()
    {
        const string plainText = "VeryStrongPassword123";
        var hash = Password.Hash(plainText);

        Assert.NotEqual(plainText, hash);
        Assert.True(Password.Verify(plainText, hash));
        Assert.False(Password.Verify("WrongPassword", hash));
    }

    [Fact]
    public async Task SetPasswordAsync_InsertsCredentialWhenMissing()
    {
        var harness = new MongoHarness();
        var service = new Password(harness.Database);
        var userId = ObjectId.GenerateNewId();
        var timestamp = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        await service.SetPasswordAsync(userId, "Password123!", timestamp);

        var stored = await service.FindCredentialAsync(userId);
        Assert.NotNull(stored);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(timestamp, stored.UpdatedAt);
        Assert.True(Password.Verify("Password123!", stored.PasswordHash));
    }

    [Fact]
    public async Task SetPasswordAsync_ReplacesCredentialWhenExisting()
    {
        var harness = new MongoHarness();
        var service = new Password(harness.Database);
        var userId = ObjectId.GenerateNewId();
        var existing = new EmailPasswordCredential
        {
            UserId = userId,
            PasswordHash = Password.Hash("OldPassword123!"),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        harness.EmailPasswordCredentials.Seed(existing);
        var timestamp = new DateTime(2026, 8, 11, 13, 0, 0, DateTimeKind.Utc);

        await service.SetPasswordAsync(userId, "NewPassword123!", timestamp);

        var stored = await service.FindCredentialAsync(userId);
        Assert.NotNull(stored);
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal(timestamp, stored.UpdatedAt);
        Assert.True(Password.Verify("NewPassword123!", stored.PasswordHash));
        Assert.Single(harness.EmailPasswordCredentials.Replaced);
    }

    [Fact]
    public async Task CreateEmailIdentityAsync_NormalizesAndPersistsIdentity()
    {
        var harness = new MongoHarness();
        var service = new Password(harness.Database);
        var userId = ObjectId.GenerateNewId();

        var identity = await service.CreateEmailIdentityAsync(userId, "  USER@Example.COM ", true);

        Assert.Equal(userId, identity.UserId);
        Assert.Equal(IdentityProvider.Email, identity.Provider);
        Assert.Equal("user@example.com", identity.ProviderSubject);
        Assert.Equal("user@example.com", identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Single(harness.AuthenticationIdentities.Documents);
    }
}