using MongoDB.Bson;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.DatabaseEntity;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Data;

public sealed class LookupTests
{
    [Fact]
    public async Task ClientsAsync_ReturnsMatchingClientsForOrganization()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var client = new SartainStudios.Schema.DatabaseEntity.Client { OrganizationId = organizationId };
        harness.Clients.Seed(client);
        var lookup = new Lookup(harness.Database);

        var result = await lookup.ClientsAsync(organizationId, [client.Id]);

        Assert.Single(result);
        Assert.Equal(client, result[client.Id]);
    }

    [Fact]
    public async Task ProjectsAsync_ReturnsEmptyWhenNoIdsProvided()
    {
        var harness = new MongoHarness();
        var lookup = new Lookup(harness.Database);

        var result = await lookup.ProjectsAsync(ObjectId.GenerateNewId(), []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ContractsAsync_ReturnsMatchingContractsForOrganization()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var contract = new BillingContract { OrganizationId = organizationId };
        harness.BillingContracts.Seed(contract);
        var lookup = new Lookup(harness.Database);

        var result = await lookup.ContractsAsync(organizationId, [contract.Id]);

        Assert.Single(result);
        Assert.Equal(contract, result[contract.Id]);
    }

    [Fact]
    public async Task OrganizationsAsync_ReturnsEmptyWhenNoIdsProvided()
    {
        var harness = new MongoHarness();
        var lookup = new Lookup(harness.Database);

        var result = await lookup.OrganizationsAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task OrganizationsAsync_ReturnsMatchingOrganizations()
    {
        var harness = new MongoHarness();
        var organization = new OrganizationEntity();
        harness.Organizations.Seed(organization);
        var lookup = new Lookup(harness.Database);

        var result = await lookup.OrganizationsAsync([organization.Id]);

        Assert.Single(result);
        Assert.Equal(organization, result[organization.Id]);
    }

    [Fact]
    public async Task InvoiceStatusesAsync_ReturnsEmptyWhenNoIdsProvided()
    {
        var harness = new MongoHarness();
        var lookup = new Lookup(harness.Database);

        var result = await lookup.InvoiceStatusesAsync(ObjectId.GenerateNewId(), [null]);

        Assert.Empty(result);
    }
}