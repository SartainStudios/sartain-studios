using MongoDB.Driver;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal sealed class MongoHarness
{
    public MongoHarness()
    {
        Client = Substitute.For<IMongoClient>();

        var mongoDatabase = Substitute.For<IMongoDatabase>();
        Client.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDatabase);

        Session = Substitute.For<IClientSessionHandle>();
        Session.IsInTransaction.Returns(_ => IsInTransaction);
        Session.When(x => x.StartTransaction(Arg.Any<TransactionOptions>())).Do(_ => IsInTransaction = true);
        Session.CommitTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                IsInTransaction = false;
                CommittedTransactionCount++;

                return Task.CompletedTask;
            });
        Session.AbortTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                IsInTransaction = false;
                AbortedTransactionCount++;

                return Task.CompletedTask;
            });

        Client.StartSessionAsync(Arg.Any<ClientSessionOptions>(), Arg.Any<CancellationToken>()).Returns(Session);

        Register(mongoDatabase, UserProfiles);
        Register(mongoDatabase, Memberships);
        Register(mongoDatabase, AuthenticationIdentities);
        Register(mongoDatabase, AuthenticationSessions);
        Register(mongoDatabase, EmailPasswordCredentials);
        Register(mongoDatabase, PasswordResetTokens);
        Register(mongoDatabase, Organizations);
        Register(mongoDatabase, Clients);
        Register(mongoDatabase, Projects);
        Register(mongoDatabase, BillingContracts);
        Register(mongoDatabase, TimeSessions);
        Register(mongoDatabase, Invoices);
        Register(mongoDatabase, InvoiceSequences);

        Database = new Database(Client, new Mongo { ConnectionUri = "mongodb://localhost", DatabaseName = "tests" });
    }

    public IMongoClient Client { get; }

    public IClientSessionHandle Session { get; }

    public Database Database { get; }

    public bool IsInTransaction { get; private set; }

    public int CommittedTransactionCount { get; private set; }

    public int AbortedTransactionCount { get; private set; }

    public FakeCollection<UserProfile> UserProfiles { get; } = new();

    public FakeCollection<SartainStudios.Schema.DatabaseEntity.Membership> Memberships { get; } = new();

    public FakeCollection<AuthenticationIdentity> AuthenticationIdentities { get; } = new();

    public FakeCollection<AuthenticationSession> AuthenticationSessions { get; } = new();

    public FakeCollection<EmailPasswordCredential> EmailPasswordCredentials { get; } = new();

    public FakeCollection<PasswordResetToken> PasswordResetTokens { get; } = new();

    public FakeCollection<OrganizationEntity> Organizations { get; } = new();

    public FakeCollection<SartainStudios.Schema.DatabaseEntity.Client> Clients { get; } = new();

    public FakeCollection<SartainStudios.Schema.DatabaseEntity.Project> Projects { get; } = new();

    public FakeCollection<BillingContract> BillingContracts { get; } = new();

    public FakeCollection<WorkSession> TimeSessions { get; } = new();

    public FakeCollection<InvoiceEntity> Invoices { get; } = new();

    public FakeCollection<InvoiceSequence> InvoiceSequences { get; } = new();

    private static void Register<TDocument>(IMongoDatabase mongoDatabase, FakeCollection<TDocument> collection)
        where TDocument : class
    {
        mongoDatabase
            .GetCollection<TDocument>(Arg.Any<string>(), Arg.Any<MongoCollectionSettings>())
            .Returns(collection.Collection);
    }
}