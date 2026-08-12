using MongoDB.Driver;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Api.Service.Data;

public sealed class IndexInitializer(Database database) : IIndexInitializer
{
    public async Task InitializeIndexesAsync()
    {
        try
        {
            await InitializeUserProfileIndexesAsync();
            await InitializeAuthenticationIdentityIndexesAsync();
            await InitializeAuthenticationSessionIndexesAsync();
            await InitializeEmailPasswordCredentialIndexesAsync();
            await InitializePasswordResetTokenIndexesAsync();
            await InitializeOrganizationIndexesAsync();
            await InitializeMembershipIndexesAsync();
            await InitializeClientIndexesAsync();
            await InitializeProjectIndexesAsync();
            await InitializeBillingContractIndexesAsync();
            await InitializeTimeSessionIndexesAsync();
            await InitializeInvoiceIndexesAsync();
            await InitializeInvoiceSequenceIndexesAsync();
            await InitializeHourLimitNotificationIndexesAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to initialize MongoDB indexes. Check your connection and database access.", ex);
        }
    }

    private async Task InitializeUserProfileIndexesAsync()
    {
        var collection = database.UserProfiles;
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<UserProfile>(
                Builders<UserProfile>.IndexKeys.Ascending(x => x.DisplayName)
            )
        );
    }

    private async Task InitializeAuthenticationIdentityIndexesAsync()
    {
        var collection = database.AuthenticationIdentities;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthenticationIdentity>(
                Builders<AuthenticationIdentity>.IndexKeys.Combine(
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.Provider),
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.ProviderSubject)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthenticationIdentity>(
                Builders<AuthenticationIdentity>.IndexKeys.Combine(
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.UserId),
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.Provider)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthenticationIdentity>(
                Builders<AuthenticationIdentity>.IndexKeys.Combine(
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.Provider),
                    Builders<AuthenticationIdentity>.IndexKeys.Ascending(x => x.Email)
                )
            )
        );
    }

    private async Task InitializeEmailPasswordCredentialIndexesAsync()
    {
        var collection = database.EmailPasswordCredentials;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<EmailPasswordCredential>(
                Builders<EmailPasswordCredential>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true }
            )
        );
    }

    private async Task InitializePasswordResetTokenIndexesAsync()
    {
        var collection = database.PasswordResetTokens;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PasswordResetToken>(
                Builders<PasswordResetToken>.IndexKeys.Ascending(x => x.TokenHash),
                new CreateIndexOptions { Unique = true }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PasswordResetToken>(
                Builders<PasswordResetToken>.IndexKeys.Ascending(x => x.UserId)
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PasswordResetToken>(
                Builders<PasswordResetToken>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }
            )
        );
    }

    private async Task InitializeAuthenticationSessionIndexesAsync()
    {
        var collection = database.AuthenticationSessions;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthenticationSession>(
                Builders<AuthenticationSession>.IndexKeys.Ascending(x => x.RefreshTokenHash),
                new CreateIndexOptions { Unique = true }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthenticationSession>(
                Builders<AuthenticationSession>.IndexKeys.Combine(
                    Builders<AuthenticationSession>.IndexKeys.Ascending(x => x.UserId),
                    Builders<AuthenticationSession>.IndexKeys.Ascending(x => x.ExpiresAt)
                )
            )
        );
    }

    private async Task InitializeOrganizationIndexesAsync()
    {
        var collection = database.Organizations;
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Organization>(
                Builders<SartainStudios.Schema.DatabaseEntity.Organization>.IndexKeys.Ascending(x => x.Name)
            )
        );
    }

    private async Task InitializeMembershipIndexesAsync()
    {
        var collection = database.Memberships;

        await DropIndexIfExistsAsync(collection, "OrganizationId_1_UserId_1");
        await DropIndexIfExistsAsync(collection, "OrganizationId_1_Email_1");

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Membership>(
                Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys
                        .Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.UserId)
                ),
                new CreateIndexOptions<SartainStudios.Schema.DatabaseEntity.Membership>
                {
                    Name = "uniq_org_user_active",
                    Unique = true,
                    PartialFilterExpression = Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.In(
                        x => x.Status, new[] { nameof(RoleStatus.Active), nameof(RoleStatus.Suspended) })
                }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Membership>(
                Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys
                        .Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.Email)
                ),
                new CreateIndexOptions<SartainStudios.Schema.DatabaseEntity.Membership>
                {
                    Name = "uniq_org_email_invited",
                    Unique = true,
                    PartialFilterExpression = Builders<SartainStudios.Schema.DatabaseEntity.Membership>.Filter.Eq(
                        x => x.Status, nameof(RoleStatus.Invited))
                }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Membership>(
                Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys
                        .Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.Email)
                ),
                new CreateIndexOptions { Name = "lookup_org_email" }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Membership>(
                Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys
                        .Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.Role),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.Status)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Membership>(
                Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.UserId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Membership>.IndexKeys.Ascending(x => x.Status)
                )
            )
        );
    }

    private async Task InitializeClientIndexesAsync()
    {
        var collection = database.Clients;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Client>(
                Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Ascending(x => x.CompanyName)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Client>(
                Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Client>.IndexKeys.Ascending(x => x.Email)
                )
            )
        );
    }

    private async Task InitializeProjectIndexesAsync()
    {
        var collection = database.Projects;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Project>(
                Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Ascending(x => x.ClientId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Ascending(x => x.Status)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Project>(
                Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Project>.IndexKeys.Ascending(x => x.Name)
                )
            )
        );
    }

    private async Task InitializeBillingContractIndexesAsync()
    {
        var collection = database.BillingContracts;

        await DropIndexIfExistsAsync(collection, "OrganizationId_1_ProjectId_1_IsActive_1");

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BillingContract>(
                Builders<BillingContract>.IndexKeys.Combine(
                    Builders<BillingContract>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<BillingContract>.IndexKeys.Ascending(x => x.ProjectId)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BillingContract>(
                Builders<BillingContract>.IndexKeys.Combine(
                    Builders<BillingContract>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<BillingContract>.IndexKeys.Ascending(x => x.ProjectId)
                ),
                new CreateIndexOptions<BillingContract>
                {
                    Name = "uniq_active_contract_per_project",
                    Unique = true,
                    PartialFilterExpression = Builders<BillingContract>.Filter.Eq(x => x.IsActive, true)
                }
            )
        );
    }

    private async Task InitializeTimeSessionIndexesAsync()
    {
        var collection = database.TimeSessions;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<WorkSession>(
                Builders<WorkSession>.IndexKeys.Combine(
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.ProjectId),
                    Builders<WorkSession>.IndexKeys.Descending(x => x.StartTime)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<WorkSession>(
                Builders<WorkSession>.IndexKeys.Combine(
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.ContractId),
                    Builders<WorkSession>.IndexKeys.Descending(x => x.StartTime)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<WorkSession>(
                Builders<WorkSession>.IndexKeys.Combine(
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.InvoiceId)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<WorkSession>(
                Builders<WorkSession>.IndexKeys.Combine(
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.EndTime),
                    Builders<WorkSession>.IndexKeys.Descending(x => x.StartTime)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<WorkSession>(
                Builders<WorkSession>.IndexKeys.Combine(
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.UserId),
                    Builders<WorkSession>.IndexKeys.Ascending(x => x.EndTime)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );
    }

    private async Task InitializeInvoiceIndexesAsync()
    {
        var collection = database.Invoices;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Invoice>(
                Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.InvoiceNumber)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Invoice>(
                Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.ClientId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Descending(x => x.CreatedAt)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Invoice>(
                Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.Status),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.DueDate)
                )
            )
        );

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<SartainStudios.Schema.DatabaseEntity.Invoice>(
                Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Combine(
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<SartainStudios.Schema.DatabaseEntity.Invoice>.IndexKeys.Descending(x => x.CreatedAt)
                )
            )
        );
    }

    private static async Task DropIndexIfExistsAsync<T>(IMongoCollection<T> collection, string indexName)
    {
        using var cursor = await collection.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        if (indexes.Any(i => i.TryGetValue("name", out var value) && value.AsString == indexName))
            await collection.Indexes.DropOneAsync(indexName);
    }

    private async Task InitializeHourLimitNotificationIndexesAsync()
    {
        var collection = database.HourLimitNotifications;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<HourLimitNotification>(
                Builders<HourLimitNotification>.IndexKeys.Combine(
                    Builders<HourLimitNotification>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<HourLimitNotification>.IndexKeys.Ascending(x => x.UserId),
                    Builders<HourLimitNotification>.IndexKeys.Ascending(x => x.WeekStart),
                    Builders<HourLimitNotification>.IndexKeys.Ascending(x => x.NotificationType)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );
    }

    private async Task InitializeInvoiceSequenceIndexesAsync()
    {
        var collection = database.InvoiceSequences;

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<InvoiceSequence>(
                Builders<InvoiceSequence>.IndexKeys.Combine(
                    Builders<InvoiceSequence>.IndexKeys.Ascending(x => x.OrganizationId),
                    Builders<InvoiceSequence>.IndexKeys.Ascending(x => x.InvoicePrefix)
                ),
                new CreateIndexOptions { Unique = true }
            )
        );
    }
}