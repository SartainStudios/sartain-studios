using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Onboarding;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using ClientEntity = SartainStudios.Schema.DatabaseEntity.Client;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;

namespace SartainStudios.Api.Service.Test.Onboarding;

public sealed class OnboardingServiceTests
{
    private static OnboardingService CreateService(MongoHarness harness, CurrentTenant tenant)
    {
        return new OnboardingService(harness.Database, tenant);
    }

    private static Address ValidAddress()
    {
        return new Address
        {
            Line1 = "123 Main St",
            City = "Springfield",
            StateOrProvince = "IL",
            PostalCode = "62704",
            Country = "USA"
        };
    }

    [Fact]
    public async Task GetAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness, TestTenant.Anonymous());

        var result = await service.GetAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsAllFalseWhenNothingProvisioned()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness, TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()));

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.OrganizationCustomized);
        Assert.False(result.Value.HasClient);
        Assert.False(result.Value.HasProject);
        Assert.False(result.Value.HasBillingContract);
        Assert.False(result.Value.HasLoggedSession);
        Assert.False(result.Value.HasInvoice);
    }

    [Fact]
    public async Task GetAsync_ReturnsCompletedStepsForActiveOrganization()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var clientId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();

        harness.Organizations.Seed(new OrganizationEntity
        {
            Id = organizationId,
            Name = "Sartain Studios",
            Address = ValidAddress(),
            PhoneNumber = "555-123-4567"
        });
        harness.Clients.Seed(new ClientEntity { Id = clientId, OrganizationId = organizationId });
        harness.Projects.Seed(new ProjectEntity
            { Id = projectId, OrganizationId = organizationId, ClientId = clientId });
        harness.BillingContracts.Seed(new BillingContract { OrganizationId = organizationId, ProjectId = projectId });
        harness.TimeSessions.Seed(new WorkSession { OrganizationId = organizationId, ProjectId = projectId });
        harness.Invoices.Seed(new InvoiceEntity { OrganizationId = organizationId, ClientId = clientId });

        var service = CreateService(harness, TestTenant.Create(ObjectId.GenerateNewId(), organizationId));

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OrganizationCustomized);
        Assert.True(result.Value.HasClient);
        Assert.True(result.Value.HasProject);
        Assert.True(result.Value.HasBillingContract);
        Assert.True(result.Value.HasLoggedSession);
        Assert.True(result.Value.HasInvoice);
    }

    [Fact]
    public async Task GetAsync_IgnoresDataFromOtherOrganizations()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var otherOrganizationId = ObjectId.GenerateNewId();

        harness.Clients.Seed(new ClientEntity
            { Id = ObjectId.GenerateNewId(), OrganizationId = otherOrganizationId });

        var service = CreateService(harness, TestTenant.Create(ObjectId.GenerateNewId(), organizationId));

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasClient);
    }

    [Fact]
    public async Task GetAsync_ReportsOrganizationNotCustomizedWhenPhoneNumberMissing()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();

        harness.Organizations.Seed(new OrganizationEntity
        {
            Id = organizationId,
            Name = "Sartain Studios",
            Address = ValidAddress()
        });

        var service = CreateService(harness, TestTenant.Create(ObjectId.GenerateNewId(), organizationId));

        var result = await service.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.OrganizationCustomized);
    }
}