using MongoDB.Driver;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Test.Data;

public sealed class DatabaseTests
{
    private static (Database Database, IMongoDatabase MongoDatabase) CreateDatabase()
    {
        var client = Substitute.For<IMongoClient>();
        var mongoDatabase = Substitute.For<IMongoDatabase>();
        client.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDatabase);
        var database = new Database(client,
            new Mongo { ConnectionUri = "mongodb://localhost", DatabaseName = "tests" });

        return (database, mongoDatabase);
    }

    private static void AssertCollection<TDocument>(
        IMongoDatabase mongoDatabase,
        string name,
        Func<IMongoCollection<TDocument>> actual) where TDocument : class
    {
        var expected = Substitute.For<IMongoCollection<TDocument>>();
        mongoDatabase.GetCollection<TDocument>(name, Arg.Any<MongoCollectionSettings>()).Returns(expected);

        Assert.Same(expected, actual());
    }

    [Fact]
    public void Collections_ReturnUnderlyingMongoCollections()
    {
        var (database, mongoDatabase) = CreateDatabase();

        AssertCollection(mongoDatabase, nameof(UserProfile), () => database.UserProfiles);
        AssertCollection(mongoDatabase, nameof(AuthenticationIdentity), () => database.AuthenticationIdentities);
        AssertCollection(mongoDatabase, nameof(AuthenticationSession), () => database.AuthenticationSessions);
        AssertCollection(mongoDatabase, nameof(EmailPasswordCredential), () => database.EmailPasswordCredentials);
        AssertCollection(mongoDatabase, nameof(PasswordResetToken), () => database.PasswordResetTokens);
        AssertCollection(mongoDatabase, nameof(Organization), () => database.Organizations);
        AssertCollection(mongoDatabase, nameof(Membership), () => database.Memberships);
        AssertCollection(mongoDatabase, nameof(Client), () => database.Clients);
        AssertCollection(mongoDatabase, nameof(Project), () => database.Projects);
        AssertCollection(mongoDatabase, nameof(BillingContract), () => database.BillingContracts);
        AssertCollection(mongoDatabase, nameof(WorkSession), () => database.TimeSessions);
        AssertCollection(mongoDatabase, nameof(Invoice), () => database.Invoices);
        AssertCollection(mongoDatabase, nameof(InvoiceSequence), () => database.InvoiceSequences);
        AssertCollection(mongoDatabase, nameof(HourLimitNotification), () => database.HourLimitNotifications);
    }
}