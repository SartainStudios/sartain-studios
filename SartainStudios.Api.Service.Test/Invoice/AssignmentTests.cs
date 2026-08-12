using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class AssignmentTests
{
    [Fact]
    public async Task LoadBilledSessionsAsync_ReturnsSessionsForInvoice()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoiceId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(new WorkSession
        {
            OrganizationId = organizationId,
            InvoiceId = invoiceId,
            StartTime = DateTime.UtcNow
        });
        var assignment = new Assignment(harness.Database);

        var result = await assignment.LoadBilledSessionsAsync(organizationId, invoiceId);

        Assert.Single(result);
    }

    [Fact]
    public async Task LoadBillableSessionsAsync_ReturnsUnbilledSessions()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var contractId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(new WorkSession
        {
            OrganizationId = organizationId,
            ContractId = contractId,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1)
        });
        var assignment = new Assignment(harness.Database);

        var result = await assignment.LoadBillableSessionsAsync(organizationId, contractId);

        Assert.Single(result);
    }

    [Fact]
    public async Task LoadSelectedSessionsAsync_ReturnsMatchingSessions()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var contractId = ObjectId.GenerateNewId();
        var session = new WorkSession
        {
            OrganizationId = organizationId,
            ContractId = contractId,
            StartTime = DateTime.UtcNow
        };
        harness.TimeSessions.Seed(session);
        var assignment = new Assignment(harness.Database);

        var result = await assignment.LoadSelectedSessionsAsync(
            harness.Session, organizationId, contractId, [session.Id]);

        Assert.Single(result);
    }
}