using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.User;

namespace SartainStudios.Api.Service.Authentication;

public sealed class Password(Database database)
{
    public const int MinimumLength = AccountErrors.MinimumPasswordLength;

    public static bool MeetsPolicy(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;
    }

    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public static bool Verify(string? password, string passwordHash)
    {
        return !string.IsNullOrWhiteSpace(password) && BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public async Task<EmailPasswordCredential?> FindCredentialAsync(ObjectId userId)
    {
        return await database.EmailPasswordCredentials
            .Find(x => x.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasPasswordAsync(ObjectId userId)
    {
        return await database.EmailPasswordCredentials
            .Find(x => x.UserId == userId)
            .AnyAsync();
    }

    public async Task SetPasswordAsync(ObjectId userId, string newPassword, DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;
        var credential = await FindCredentialAsync(userId);
        if (credential is null)
        {
            await database.EmailPasswordCredentials.InsertOneAsync(new EmailPasswordCredential
            {
                UserId = userId,
                PasswordHash = Hash(newPassword),
                UpdatedAt = now
            });
            return;
        }

        credential.PasswordHash = Hash(newPassword);
        credential.UpdatedAt = now;
        await database.EmailPasswordCredentials.ReplaceOneAsync(x => x.Id == credential.Id, credential);
    }

    public async Task<bool> EmailIdentityExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await database.AuthenticationIdentities
            .Find(x => x.Provider == IdentityProvider.Email && x.ProviderSubject == normalizedEmail)
            .AnyAsync();
    }

    public async Task<AuthenticationIdentity> CreateEmailIdentityAsync(ObjectId userId, string email,
        bool emailVerified, DateTime? timestamp = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var identity = new AuthenticationIdentity
        {
            UserId = userId,
            Provider = IdentityProvider.Email,
            ProviderSubject = normalizedEmail,
            Email = normalizedEmail,
            EmailVerified = emailVerified,
            UpdatedAt = timestamp ?? DateTime.UtcNow
        };
        await database.AuthenticationIdentities.InsertOneAsync(identity);
        return identity;
    }
}