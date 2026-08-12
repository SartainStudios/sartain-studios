using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Client;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Client;
using ClientEntity = SartainStudios.Schema.DatabaseEntity.Client;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;

namespace SartainStudios.Api.Service.Test.Clients;

public sealed class ClientServiceTests
{
    private static ClientService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        return new ClientService(
            harness.Database,
            tenant ?? TestTenant.Create(ObjectId.GenerateNewId(), ObjectId.GenerateNewId()),
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow));
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

    private static CreateRequest ValidCreateRequest()
    {
        return new CreateRequest("Acme Inc", "Jane Doe", ValidAddress(), "jane@acme.com", "555-123-4567");
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
    public async Task ListAsync_ReturnsEmptyWhenNoClients()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetAsync("not-an-id");

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.InvalidId, result.Error);
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
    public async Task CreateAsync_ReturnsValidationErrorWhenCompanyNameMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = ValidCreateRequest() with { CompanyName = "" };

        var result = await service.CreateAsync(request);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CreateAsync_CreatesClientWhenValid()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var request = ValidCreateRequest();

        var result = await service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Single(harness.Clients.Documents);
        Assert.Equal("Acme Inc", result.Value.CompanyName);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = new UpdateRequest("Acme Inc", "Jane Doe", ValidAddress(), "jane@acme.com", "555-123-4567");

        var result = await service.UpdateAsync(ObjectId.GenerateNewId().ToString(), request);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesClientWhenValid()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var client = new ClientEntity
        {
            OrganizationId = organizationId,
            CompanyName = "Old Name",
            ContactPerson = "Jane Doe",
            Address = ValidAddress(),
            Email = "jane@acme.com",
            PhoneNumber = "555-123-4567"
        };
        harness.Clients.Seed(client);
        var request = new UpdateRequest(
            "New Name", "Jane Doe", ValidAddress(), "jane@acme.com", "555-123-4567");

        var result = await service.UpdateAsync(client.Id.ToString(), request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.CompanyName);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsHasProjectsWhenProjectsExist()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var client = new ClientEntity { OrganizationId = organizationId };
        harness.Clients.Seed(client);
        harness.Projects.Seed(new ProjectEntity
        {
            OrganizationId = organizationId,
            ClientId = client.Id,
            Name = "My Project"
        });

        var result = await service.DeleteAsync(client.Id.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.HasProjects, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_DeletesClientWhenNoProjects()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var client = new ClientEntity { OrganizationId = organizationId };
        harness.Clients.Seed(client);

        var result = await service.DeleteAsync(client.Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.Clients.Documents);
    }
}