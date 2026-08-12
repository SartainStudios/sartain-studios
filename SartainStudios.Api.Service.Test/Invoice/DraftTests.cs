using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using Status = SartainStudios.Schema.Invoice.Status;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class DraftTests
{
    [Fact]
    public void IsDraft_ReturnsTrueForDraftStatus()
    {
        var invoice = new InvoiceEntity { Status = nameof(Status.Draft) };

        Assert.True(Draft.IsDraft(invoice));
    }

    [Fact]
    public void IsDraft_ReturnsFalseForSentStatus()
    {
        var invoice = new InvoiceEntity { Status = nameof(Status.Sent) };

        Assert.False(Draft.IsDraft(invoice));
    }

    [Fact]
    public void CanTransitionStatus_ReturnsTrueForAllowedTransition()
    {
        Assert.True(Draft.CanTransitionStatus(nameof(Status.Draft), nameof(Status.Sent)));
    }

    [Fact]
    public void CanTransitionStatus_ReturnsFalseForDisallowedTransition()
    {
        Assert.False(Draft.CanTransitionStatus(nameof(Status.Paid), nameof(Status.Draft)));
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullWhenInvoiceMissing()
    {
        var harness = new MongoHarness();
        var draft = new Draft(harness.Database);

        var result = await draft.LoadAsync(ObjectId.GenerateNewId(), ObjectId.GenerateNewId());

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ReturnsInvoiceWhenDraft()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = new InvoiceEntity { OrganizationId = organizationId, Status = nameof(Status.Draft) };
        harness.Invoices.Seed(invoice);
        var draft = new Draft(harness.Database);

        var result = await draft.LoadAsync(organizationId, invoice.Id);

        Assert.NotNull(result);
        Assert.Equal(invoice.Id, result!.Id);
    }
}