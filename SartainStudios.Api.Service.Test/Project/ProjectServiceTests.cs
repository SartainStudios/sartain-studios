using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Project;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Project;
using BillingContractEntity = SartainStudios.Schema.DatabaseEntity.BillingContract;
using ClientEntity = SartainStudios.Schema.DatabaseEntity.Client;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Project;

public sealed class ProjectServiceTests
{
    private static ProjectService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        return new ProjectService(
            harness.Database,
            tenant ?? TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()),
            new Lookup(harness.Database),
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow));
    }

    private static CreateRequest ValidCreateRequest(string clientId)
    {
        return new CreateRequest(clientId, "  Site Revamp  ", "  Migration work  ", "active");
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
    public async Task ListAsync_ReturnsSummariesForTenantProjects()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var otherOrganizationId = ObjectId.GenerateNewId();
        var clientId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);

        harness.Clients.Seed(
            new ClientEntity { Id = clientId, OrganizationId = organizationId, CompanyName = "Acme" },
            new ClientEntity
                { Id = ObjectId.GenerateNewId(), OrganizationId = otherOrganizationId, CompanyName = "Other" });

        harness.Projects.Seed(
            new ProjectEntity
            {
                OrganizationId = organizationId,
                ClientId = clientId,
                Name = "Alpha",
                Description = "Tenant project",
                Status = nameof(Status.Active)
            },
            new ProjectEntity
            {
                OrganizationId = otherOrganizationId,
                ClientId = clientId,
                Name = "Zulu",
                Description = "Other tenant project",
                Status = nameof(Status.Active)
            });

        var result = await service.ListAsync();

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(result.Value);
        Assert.Equal("Alpha", summary.Name);
        Assert.Equal("Acme", summary.ClientCompanyName);
    }

    [Fact]
    public async Task GetAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetAsync("not-an-id");

        Assert.True(result.IsFailure);
        Assert.Equal(ProjectErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsSummaryWhenProjectExists()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var clientId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);

        harness.Clients.Seed(new ClientEntity
        {
            Id = clientId,
            OrganizationId = organizationId,
            CompanyName = "Acme"
        });

        harness.Projects.Seed(new ProjectEntity
        {
            Id = projectId,
            OrganizationId = organizationId,
            ClientId = clientId,
            Name = "Project",
            Description = "Description",
            Status = nameof(Status.Active)
        });

        var result = await service.GetAsync(projectId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(projectId.ToString(), result.Value.Id);
        Assert.Equal("Acme", result.Value.ClientCompanyName);
    }

    [Fact]
    public async Task CreateAsync_CreatesProjectWhenRequestIsValid()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var organizationId = ObjectId.GenerateNewId();
        var clientId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));

        harness.Clients.Seed(new ClientEntity
        {
            Id = clientId,
            OrganizationId = organizationId,
            CompanyName = "Acme"
        });

        var result = await service.CreateAsync(ValidCreateRequest(clientId.ToString()));

        Assert.True(result.IsSuccess);
        var project = Assert.Single(harness.Projects.Documents);
        Assert.Equal(organizationId, project.OrganizationId);
        Assert.Equal(clientId, project.ClientId);
        Assert.Equal("Site Revamp", project.Name);
        Assert.Equal("Migration work", project.Description);
        Assert.Equal(nameof(Status.Active), project.Status);
        Assert.Equal(now.UtcDateTime, project.CreatedAt);
        Assert.Equal(now.UtcDateTime, project.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var request = new UpdateRequest(ObjectId.GenerateNewId().ToString(), "Name", "Description",
            nameof(Status.Active));

        var result = await service.UpdateAsync(ObjectId.GenerateNewId().ToString(), request);

        Assert.True(result.IsFailure);
        Assert.Equal("Project.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesProjectWhenRequestIsValid()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.Zero);
        var organizationId = ObjectId.GenerateNewId();
        var clientId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant, new StaticTimeProvider(now));

        harness.Clients.Seed(new ClientEntity
        {
            Id = clientId,
            OrganizationId = organizationId,
            CompanyName = "Acme"
        });

        harness.Projects.Seed(new ProjectEntity
        {
            Id = projectId,
            OrganizationId = organizationId,
            ClientId = ObjectId.GenerateNewId(),
            Name = "Old",
            Description = "Old",
            Status = nameof(Status.Archived),
            UpdatedAt = now.AddHours(-1).UtcDateTime
        });

        var request = new UpdateRequest(clientId.ToString(), "  New Name  ", "  New Description  ", "active");

        var result = await service.UpdateAsync(projectId.ToString(), request);

        Assert.True(result.IsSuccess);
        var project = Assert.Single(harness.Projects.Documents);
        Assert.Equal("New Name", project.Name);
        Assert.Equal("New Description", project.Description);
        Assert.Equal(nameof(Status.Active), project.Status);
        Assert.Equal(clientId, project.ClientId);
        Assert.Equal(now.UtcDateTime, project.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsHasBillingContractsWhenContractsExist()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);

        harness.Projects.Seed(new ProjectEntity
        {
            Id = projectId,
            OrganizationId = organizationId,
            ClientId = ObjectId.GenerateNewId(),
            Name = "Project",
            Description = "Description",
            Status = nameof(Status.Active)
        });

        harness.BillingContracts.Seed(new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = projectId
        });

        var result = await service.DeleteAsync(projectId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(ProjectErrors.HasBillingContracts, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsHasWorkSessionsWhenSessionsExist()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);

        harness.Projects.Seed(new ProjectEntity
        {
            Id = projectId,
            OrganizationId = organizationId,
            ClientId = ObjectId.GenerateNewId(),
            Name = "Project",
            Description = "Description",
            Status = nameof(Status.Active)
        });

        harness.TimeSessions.Seed(new WorkSessionEntity
        {
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = projectId
        });

        var result = await service.DeleteAsync(projectId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(ProjectErrors.HasWorkSessions, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_DeletesProjectWhenNoDependencies()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var projectId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);

        harness.Projects.Seed(new ProjectEntity
        {
            Id = projectId,
            OrganizationId = organizationId,
            ClientId = ObjectId.GenerateNewId(),
            Name = "Project",
            Description = "Description",
            Status = nameof(Status.Active)
        });

        harness.TimeSessions.Seed(new WorkSessionEntity
        {
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId()
        });

        var result = await service.DeleteAsync(projectId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.Projects.Documents);
    }
}