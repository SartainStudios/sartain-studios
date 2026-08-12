using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Billing;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Billing;
using SartainStudios.Schema.DatabaseEntity;
using BillingContractEntity = SartainStudios.Schema.DatabaseEntity.BillingContract;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;

namespace SartainStudios.Api.Service.Test.Billing;

public sealed class BillingContractServiceTests
{
    private static BillingContractService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        return new BillingContractService(
            harness.Database,
            tenant ?? TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()),
            new Lookup(harness.Database),
            new Sequence(harness.Database),
            new Draft(harness.Database),
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow));
    }

    private static CreateRequest ValidCreateRequest(string projectId)
    {
        return new CreateRequest(projectId, 100m, 60, nameof(Cycle.Monthly), "Consulting", "INV", true);
    }

    [Fact]
    public async Task ListAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness, TestTenant.Anonymous());

        var result = await service.ListAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyWhenNoContracts()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListAsync_ReturnsInvalidProjectIdWhenProjectIdMalformed()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.ListAsync("not-an-id");

        Assert.True(result.IsFailure);
        Assert.Equal(BillingContractErrors.InvalidProjectId, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetAsync("not-an-id");

        Assert.True(result.IsFailure);
        Assert.Equal(BillingContractErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetAsync(ObjectId.GenerateNewId().ToString());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorWhenProjectIdMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = ValidCreateRequest("");

        var result = await service.CreateAsync(request);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_ReturnsProjectNotFoundWhenProjectMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = ValidCreateRequest(ObjectId.GenerateNewId().ToString());

        var result = await service.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingContractErrors.ProjectNotFound, result.Error);
    }

    [Fact]
    public async Task CreateAsync_CreatesContractWhenValid()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var project = new ProjectEntity { OrganizationId = organizationId, Name = "My Project" };
        harness.Projects.Seed(project);
        var request = ValidCreateRequest(project.Id.ToString());

        var result = await service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Single(harness.BillingContracts.Documents);
        Assert.Equal("My Project", result.Value.ProjectName);
    }

    [Fact]
    public async Task CreateAsync_ReturnsActiveContractExistsWhenActiveContractAlreadyPresent()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var project = new ProjectEntity { OrganizationId = organizationId, Name = "My Project" };
        harness.Projects.Seed(project);
        harness.BillingContracts.Seed(new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = project.Id,
            IsActive = true,
            BillingCycle = nameof(Cycle.Monthly),
            ServiceProvided = "Consulting",
            InvoicePrefix = "INV"
        });
        var request = ValidCreateRequest(project.Id.ToString());

        var result = await service.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingContractErrors.ActiveContractExists, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = ValidCreateRequest(ObjectId.GenerateNewId().ToString());

        var result = await service.UpdateAsync(ObjectId.GenerateNewId().ToString(),
            new UpdateRequest(request.ProjectId, request.HourlyRate, request.ExpectedMinutes,
                request.BillingCycle, request.ServiceProvided, request.InvoicePrefix, request.IsActive));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContractWhenValid()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var project = new ProjectEntity { OrganizationId = organizationId, Name = "My Project" };
        harness.Projects.Seed(project);
        var contract = new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = project.Id,
            IsActive = true,
            HourlyRate = 50m,
            BillingCycle = nameof(Cycle.Monthly),
            ServiceProvided = "Consulting",
            InvoicePrefix = "INV"
        };
        harness.BillingContracts.Seed(contract);
        var request = new UpdateRequest(project.Id.ToString(), 150m, 90, nameof(Cycle.Weekly), "Support", "SUP",
            true);

        var result = await service.UpdateAsync(contract.Id.ToString(), request);

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value.HourlyRate);
        Assert.Equal(nameof(Cycle.Weekly), result.Value.BillingCycle);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsHasWorkSessionsWhenSessionsExist()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var contract = new BillingContractEntity
            { OrganizationId = organizationId, ProjectId = ObjectId.GenerateNewId() };
        harness.BillingContracts.Seed(contract);
        harness.TimeSessions.Seed(new WorkSession
        {
            OrganizationId = organizationId,
            ContractId = contract.Id
        });

        var result = await service.DeleteAsync(contract.Id.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(BillingContractErrors.HasWorkSessions, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_DeletesContractWhenNoWorkSessions()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var contract = new BillingContractEntity
            { OrganizationId = organizationId, ProjectId = ObjectId.GenerateNewId() };
        harness.BillingContracts.Seed(contract);

        var result = await service.DeleteAsync(contract.Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.BillingContracts.Documents);
    }
}