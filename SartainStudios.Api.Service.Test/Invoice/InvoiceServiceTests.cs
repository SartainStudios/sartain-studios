using MongoDB.Bson;
using NSubstitute;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Invoice;
using SartainStudios.Schema.Membership;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using Status = SartainStudios.Schema.Invoice.Status;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class InvoiceServiceTests
{
    private static InvoiceService CreateService(MongoHarness harness, CurrentTenant? tenant = null)
    {
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var resolvedTenant = tenant ?? TestTenant.Create(userId, organizationId);

        if (resolvedTenant.TryGetIdentity(out var actualUserId, out var actualOrgId))
        {
            var membership = new MembershipEntity
            {
                OrganizationId = actualOrgId,
                UserId = actualUserId,
                Status = nameof(RoleStatus.Active),
                Role = "Owner",
                Email = "test@example.com"
            };
            harness.Memberships.Seed(membership);
        }

        var access = new Access(harness.Database, resolvedTenant);
        var assignment = new Assignment(harness.Database);
        var sequence = new Sequence(harness.Database);
        var email = Substitute.For<IEmail>();
        return new InvoiceService(
            harness.Database, access, harness.Client, assignment, sequence, email,
            new StaticTimeProvider(DateTimeOffset.UtcNow));
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
    public async Task ListAsync_ReturnsValidationErrorWhenTakeOutOfRange()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.ListAsync(take: 0);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyWhenNoInvoices()
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

        var result = await service.GetAsync("not-an-id", "America/Chicago");

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetAsync(ObjectId.GenerateNewId().ToString(), "America/Chicago");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.DeleteAsync(ObjectId.GenerateNewId().ToString());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotDeletableWhenInvoiceNotDraft()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var tenant = TestTenant.Create(ObjectId.GenerateNewId(), organizationId);
        var service = CreateService(harness, tenant);
        var invoice = new InvoiceEntity { OrganizationId = organizationId, Status = nameof(Status.Sent) };
        harness.Invoices.Seed(invoice);

        var result = await service.DeleteAsync(invoice.Id.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.NotDeletable, result.Error);
    }

    [Fact]
    public async Task EditAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = new EditRequest([ObjectId.GenerateNewId().ToString()], DateTime.UtcNow);

        var result = await service.EditAsync("not-an-id", request, "America/Chicago");

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsValidationErrorWhenContractIdMissing()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = new CreateRequest("", [ObjectId.GenerateNewId().ToString()], DateTime.UtcNow);

        var result = await service.GenerateAsync(request, "America/Chicago");

        Assert.True(result.IsFailure);
    }
}