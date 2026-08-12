using MongoDB.Driver;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Data;

public sealed class Database(IMongoClient mongoClient, Mongo mongoSettings)
{
    private readonly IMongoDatabase _database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

    public IMongoCollection<UserProfile> UserProfiles =>
        _database.GetCollection<UserProfile>(nameof(UserProfile));

    public IMongoCollection<AuthenticationIdentity> AuthenticationIdentities =>
        _database.GetCollection<AuthenticationIdentity>(nameof(AuthenticationIdentity));

    public IMongoCollection<AuthenticationSession> AuthenticationSessions =>
        _database.GetCollection<AuthenticationSession>(nameof(AuthenticationSession));

    public IMongoCollection<EmailPasswordCredential> EmailPasswordCredentials =>
        _database.GetCollection<EmailPasswordCredential>(nameof(EmailPasswordCredential));

    public IMongoCollection<PasswordResetToken> PasswordResetTokens =>
        _database.GetCollection<PasswordResetToken>(nameof(PasswordResetToken));

    public IMongoCollection<SartainStudios.Schema.DatabaseEntity.Organization> Organizations =>
        _database.GetCollection<SartainStudios.Schema.DatabaseEntity.Organization>(
            nameof(SartainStudios.Schema.DatabaseEntity.Organization));

    public IMongoCollection<SartainStudios.Schema.DatabaseEntity.Membership> Memberships =>
        _database.GetCollection<SartainStudios.Schema.DatabaseEntity.Membership>(
            nameof(SartainStudios.Schema.DatabaseEntity.Membership));

    public IMongoCollection<SartainStudios.Schema.DatabaseEntity.Client> Clients =>
        _database.GetCollection<SartainStudios.Schema.DatabaseEntity.Client>(nameof(SartainStudios.Schema.DatabaseEntity
            .Client));

    public IMongoCollection<SartainStudios.Schema.DatabaseEntity.Project> Projects =>
        _database.GetCollection<SartainStudios.Schema.DatabaseEntity.Project>(
            nameof(SartainStudios.Schema.DatabaseEntity.Project));

    public IMongoCollection<BillingContract> BillingContracts =>
        _database.GetCollection<BillingContract>(nameof(BillingContract));

    public IMongoCollection<WorkSession> TimeSessions =>
        _database.GetCollection<WorkSession>(nameof(WorkSession));

    public IMongoCollection<SartainStudios.Schema.DatabaseEntity.Invoice> Invoices =>
        _database.GetCollection<SartainStudios.Schema.DatabaseEntity.Invoice>(
            nameof(SartainStudios.Schema.DatabaseEntity.Invoice));

    public IMongoCollection<InvoiceSequence> InvoiceSequences =>
        _database.GetCollection<InvoiceSequence>(nameof(InvoiceSequence));

    public IMongoCollection<HourLimitNotification> HourLimitNotifications =>
        _database.GetCollection<HourLimitNotification>(nameof(HourLimitNotification));
}