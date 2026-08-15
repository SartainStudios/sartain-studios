using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Api.Service.Timekeeping;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using ProjectSnapshot = SartainStudios.Schema.Project.Snapshot;
using Status = SartainStudios.Schema.Invoice.Status;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class EditingTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private static Editing CreateEditing(MongoHarness harness)
    {
        return new Editing(harness.Database, harness.Client, new Draft(harness.Database));
    }

    private static WorkSessionEntity Session(
        ObjectId organizationId,
        DateTime startTime,
        DateTime? endTime,
        ObjectId? sessionId = null,
        ObjectId? invoiceId = null)
    {
        return new WorkSessionEntity
        {
            Id = sessionId ?? ObjectId.GenerateNewId(),
            OrganizationId = organizationId,
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = startTime,
            EndTime = endTime,
            InvoiceId = invoiceId
        };
    }

    private static InvoiceEntity DraftInvoice(ObjectId organizationId, decimal hourlyRate = 100m)
    {
        return new InvoiceEntity
        {
            OrganizationId = organizationId,
            Status = nameof(Status.Draft),
            ProjectSnapshot = new ProjectSnapshot { HourlyRate = hourlyRate }
        };
    }

    [Fact]
    public async Task TryReplaceUnbilledAsync_ReplacesSessionAndReturnsTrue()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, Now.AddHours(-2), Now.AddHours(-1), sessionId));
        var updated = Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), sessionId);
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceUnbilledAsync(updated);

        Assert.True(result);
        Assert.Same(updated, Assert.Single(harness.TimeSessions.Documents));
    }

    [Fact]
    public async Task TryReplaceUnbilledAsync_ReturnsFalseWhenSessionIsAlreadyBilled()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, Now.AddHours(-2), Now.AddHours(-1), sessionId,
            ObjectId.GenerateNewId()));
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceUnbilledAsync(Session(organizationId, Now.AddHours(-3), Now, sessionId));

        Assert.False(result);
        Assert.Empty(harness.TimeSessions.Replaced);
    }

    [Fact]
    public async Task TryReplaceUnbilledAsync_ReturnsFalseWhenSessionMissing()
    {
        var harness = new MongoHarness();
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceUnbilledAsync(
            Session(ObjectId.GenerateNewId(), Now.AddHours(-2), Now.AddHours(-1)));

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteUnbilledAsync_RemovesSession()
    {
        var harness = new MongoHarness();
        var session = Session(ObjectId.GenerateNewId(), Now.AddHours(-2), Now.AddHours(-1));
        harness.TimeSessions.Seed(session);
        var editing = CreateEditing(harness);

        await editing.DeleteUnbilledAsync(session);

        Assert.Empty(harness.TimeSessions.Documents);
    }

    [Fact]
    public async Task TryReplaceOnDraftInvoiceAsync_ReplacesSessionRecalculatesInvoiceAndCommits()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var sessionId = ObjectId.GenerateNewId();
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), sessionId, invoice.Id));
        var updated = Session(organizationId, Now.AddHours(-2), Now.AddHours(-1), sessionId, invoice.Id);
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceOnDraftInvoiceAsync(updated, invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.True(result);
        Assert.Equal(1, harness.CommittedTransactionCount);
        Assert.Equal(0, harness.AbortedTransactionCount);
        Assert.Equal(60, invoice.TotalMinutesWorked);
        Assert.Equal(100m, invoice.TotalAmount);
        Assert.Equal([sessionId], invoice.BilledSessionIds);
    }

    [Fact]
    public async Task TryReplaceOnDraftInvoiceAsync_AbortsAndReturnsFalseWhenSessionIsNotOnTheInvoice()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var sessionId = ObjectId.GenerateNewId();
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), sessionId));
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceOnDraftInvoiceAsync(
            Session(organizationId, Now.AddHours(-2), Now.AddHours(-1), sessionId, invoice.Id),
            invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.False(result);
        Assert.Equal(1, harness.AbortedTransactionCount);
        Assert.Equal(0, harness.CommittedTransactionCount);
    }

    [Fact]
    public async Task TryReplaceOnDraftInvoiceAsync_AbortsAndReturnsFalseWhenTheWriteFails()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var sessionId = ObjectId.GenerateNewId();
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), sessionId, invoice.Id));
        harness.TimeSessions.WriteFailure = MongoErrors.Uncategorized();
        var editing = CreateEditing(harness);

        var result = await editing.TryReplaceOnDraftInvoiceAsync(
            Session(organizationId, Now.AddHours(-2), Now.AddHours(-1), sessionId, invoice.Id), invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.False(result);
        Assert.Equal(1, harness.AbortedTransactionCount);
        Assert.Equal(0, harness.CommittedTransactionCount);
    }

    [Fact]
    public async Task TryDiscardFromDraftInvoiceAsync_RemovesSessionRecalculatesInvoiceAndCommits()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var discarded = Session(organizationId, Now.AddHours(-6), Now.AddHours(-5), invoiceId: invoice.Id);
        var retained = Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), invoiceId: invoice.Id);
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(discarded, retained);
        var editing = CreateEditing(harness);

        var result = await editing.TryDiscardFromDraftInvoiceAsync(discarded, invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.True(result);
        Assert.Equal(1, harness.CommittedTransactionCount);
        Assert.Same(retained, Assert.Single(harness.TimeSessions.Documents));
        Assert.Equal(120, invoice.TotalMinutesWorked);
        Assert.Equal(200m, invoice.TotalAmount);
        Assert.Equal([retained.Id], invoice.BilledSessionIds);
        Assert.Single(harness.Invoices.Documents);
    }

    [Fact]
    public async Task TryDiscardFromDraftInvoiceAsync_DeletesInvoiceWhenNoSessionsRemain()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var session = Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), invoiceId: invoice.Id);
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(session);
        var editing = CreateEditing(harness);

        var result = await editing.TryDiscardFromDraftInvoiceAsync(session, invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.True(result);
        Assert.Equal(1, harness.CommittedTransactionCount);
        Assert.Empty(harness.TimeSessions.Documents);
        Assert.Empty(harness.Invoices.Documents);
    }

    [Fact]
    public async Task TryDiscardFromDraftInvoiceAsync_AbortsAndReturnsFalseWhenSessionIsNotOnTheInvoice()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var session = Session(organizationId, Now.AddHours(-3), Now.AddHours(-1),
            invoiceId: ObjectId.GenerateNewId());
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(session);
        var editing = CreateEditing(harness);

        var result = await editing.TryDiscardFromDraftInvoiceAsync(session, invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.False(result);
        Assert.Equal(1, harness.AbortedTransactionCount);
        Assert.Equal(0, harness.CommittedTransactionCount);
        Assert.Single(harness.TimeSessions.Documents);
    }

    [Fact]
    public async Task TryDiscardFromDraftInvoiceAsync_AbortsAndReturnsFalseWhenTheDeleteFails()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var invoice = DraftInvoice(organizationId);
        var session = Session(organizationId, Now.AddHours(-3), Now.AddHours(-1), invoiceId: invoice.Id);
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(session);
        harness.TimeSessions.WriteFailure = MongoErrors.Uncategorized();
        var editing = CreateEditing(harness);

        var result = await editing.TryDiscardFromDraftInvoiceAsync(session, invoice,
            TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.False(result);
        Assert.Equal(1, harness.AbortedTransactionCount);
        Assert.Equal(0, harness.CommittedTransactionCount);
    }
}