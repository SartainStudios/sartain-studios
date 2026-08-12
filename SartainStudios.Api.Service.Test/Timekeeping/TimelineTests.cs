using MongoDB.Bson;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Api.Service.Timekeeping;
using BillingContractEntity = SartainStudios.Schema.DatabaseEntity.BillingContract;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using Status = SartainStudios.Schema.Invoice.Status;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class TimelineTests
{
    private const int DayTargetMinutes = 8 * 60;
    private const int WeekTargetMinutes = 40 * 60;

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private static Timeline CreateTimeline(MongoHarness harness)
    {
        return new Timeline(harness.Database, new Lookup(harness.Database));
    }

    private static WorkSessionEntity Session(
        TenantContext context,
        DateTime startTime,
        DateTime? endTime = null,
        ObjectId? contractId = null,
        ObjectId? projectId = null,
        ObjectId? invoiceId = null)
    {
        return new WorkSessionEntity
        {
            OrganizationId = context.OrganizationId,
            UserId = context.UserId,
            ContractId = contractId ?? ObjectId.GenerateNewId(),
            ProjectId = projectId ?? ObjectId.GenerateNewId(),
            StartTime = startTime,
            EndTime = endTime,
            InvoiceId = invoiceId
        };
    }

    private static TenantContext Tenant()
    {
        return new TenantContext(ObjectId.GenerateNewId(), ObjectId.GenerateNewId());
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyWhenNoSessionsExist()
    {
        var harness = new MongoHarness();
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(Tenant(), null, 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_ProjectsSessionsWithContractAndProjectDetails()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var project = new ProjectEntity { OrganizationId = context.OrganizationId, Name = "Apollo" };
        var contract = new BillingContractEntity
        {
            OrganizationId = context.OrganizationId,
            ProjectId = project.Id,
            ServiceProvided = "Engineering"
        };
        harness.Projects.Seed(project);
        harness.BillingContracts.Seed(contract);
        harness.TimeSessions.Seed(Session(context, Now.AddHours(-2), Now.AddHours(-1), contract.Id, project.Id));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, null, 10);

        var history = Assert.Single(result);
        Assert.Equal("Apollo", history.ProjectName);
        Assert.Equal("Engineering", history.ServiceProvided);
        Assert.Equal(60, history.ElapsedMinutes);
        Assert.False(history.IsRunning);
        Assert.True(history.CanEdit);
        Assert.True(history.CanDiscard);
    }

    [Fact]
    public async Task ListAsync_FlagsRunningSessions()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        harness.TimeSessions.Seed(Session(context, Now.AddHours(-1)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, null, 10);

        var history = Assert.Single(result);
        Assert.True(history.IsRunning);
        Assert.Null(history.EndTime);
        Assert.Equal(string.Empty, history.ProjectName);
    }

    [Fact]
    public async Task ListAsync_FiltersByContract()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var wantedContractId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(
            Session(context, Now.AddHours(-2), Now.AddHours(-1), wantedContractId),
            Session(context, Now.AddHours(-4), Now.AddHours(-3)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, wantedContractId, 10);

        var history = Assert.Single(result);
        Assert.Equal(wantedContractId.ToString(), history.ContractId);
    }

    [Fact]
    public async Task ListAsync_ExcludesSessionsOwnedByOtherUsersAndOrganizations()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        harness.TimeSessions.Seed(
            Session(new TenantContext(ObjectId.GenerateNewId(), context.OrganizationId), Now.AddHours(-1), Now),
            Session(new TenantContext(context.UserId, ObjectId.GenerateNewId()), Now.AddHours(-1), Now));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, null, 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_MarksSessionOnDraftInvoiceAsEditable()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var invoice = new InvoiceEntity { OrganizationId = context.OrganizationId, Status = nameof(Status.Draft) };
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(Session(context, Now.AddHours(-2), Now.AddHours(-1), invoiceId: invoice.Id));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, null, 10);

        var history = Assert.Single(result);
        Assert.Equal(invoice.Id.ToString(), history.InvoiceId);
        Assert.True(history.CanEdit);
        Assert.True(history.CanDiscard);
    }

    [Fact]
    public async Task ListAsync_MarksSessionOnSentInvoiceAsLocked()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var invoice = new InvoiceEntity { OrganizationId = context.OrganizationId, Status = nameof(Status.Sent) };
        harness.Invoices.Seed(invoice);
        harness.TimeSessions.Seed(Session(context, Now.AddHours(-2), Now.AddHours(-1), invoiceId: invoice.Id));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ListAsync(context, null, 10);

        var history = Assert.Single(result);
        Assert.False(history.CanEdit);
        Assert.False(history.CanDiscard);
    }

    [Fact]
    public async Task ProgressAsync_ReturnsEmptyWhenOnlyRunningSessionsExist()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        harness.TimeSessions.Seed(Session(context, Now.AddHours(-1)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ProgressAsync(context, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ProgressAsync_SumsLoggedMinutesPerContract()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var firstContractId = ObjectId.GenerateNewId();
        var secondContractId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(
            Session(context, Now.AddHours(-4), Now.AddHours(-3), firstContractId),
            Session(context, Now.AddHours(-2), Now.AddHours(-1), firstContractId),
            Session(context, Now.AddMinutes(-30), Now, secondContractId));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ProgressAsync(context, null);

        Assert.Equal(2, result.Count);
        Assert.Equal(120, result.Single(progress => progress.ContractId == firstContractId.ToString()).LoggedMinutes);
        Assert.Equal(30, result.Single(progress => progress.ContractId == secondContractId.ToString()).LoggedMinutes);
    }

    [Fact]
    public async Task ProgressAsync_FiltersByContract()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var wantedContractId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(
            Session(context, Now.AddHours(-2), Now.AddHours(-1), wantedContractId),
            Session(context, Now.AddHours(-4), Now.AddHours(-3)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.ProgressAsync(context, wantedContractId);

        var progress = Assert.Single(result);
        Assert.Equal(wantedContractId.ToString(), progress.ContractId);
        Assert.Equal(60, progress.LoggedMinutes);
    }

    [Fact]
    public async Task CalculateBudgetAsync_ReturnsFullRemainingWhenNothingLogged()
    {
        var harness = new MongoHarness();
        var timeline = CreateTimeline(harness);
        var dayStart = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = await timeline.CalculateBudgetAsync(Tenant(), dayStart, dayStart.AddDays(1), weekStart,
            weekStart.AddDays(7));

        Assert.Equal(0, result.DayWorkedMinutes);
        Assert.Equal(DayTargetMinutes, result.DayTargetMinutes);
        Assert.Equal(DayTargetMinutes, result.DayRemainingMinutes);
        Assert.Equal(0, result.WeekWorkedMinutes);
        Assert.Equal(WeekTargetMinutes, result.WeekTargetMinutes);
        Assert.Equal(WeekTargetMinutes, result.WeekRemainingMinutes);
    }

    [Fact]
    public async Task CalculateBudgetAsync_CountsOverlapWithDayAndWeekWindows()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var dayStart = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        harness.TimeSessions.Seed(
            Session(context, dayStart.AddHours(9), dayStart.AddHours(11)),
            Session(context, weekStart.AddHours(9), weekStart.AddHours(12)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.CalculateBudgetAsync(context, dayStart, dayStart.AddDays(1), weekStart,
            weekStart.AddDays(7));

        Assert.Equal(120, result.DayWorkedMinutes);
        Assert.Equal(DayTargetMinutes - 120, result.DayRemainingMinutes);
        Assert.Equal(300, result.WeekWorkedMinutes);
        Assert.Equal(WeekTargetMinutes - 300, result.WeekRemainingMinutes);
    }

    [Fact]
    public async Task CalculateBudgetAsync_ClipsSessionsToTheRequestedWindow()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var dayStart = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        harness.TimeSessions.Seed(Session(context, dayStart.AddHours(-2), dayStart.AddHours(1)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.CalculateBudgetAsync(context, dayStart, dayStart.AddDays(1), weekStart,
            weekStart.AddDays(7));

        Assert.Equal(60, result.DayWorkedMinutes);
        Assert.Equal(180, result.WeekWorkedMinutes);
    }

    [Fact]
    public async Task CalculateBudgetAsync_NeverReportsNegativeRemainingMinutes()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var dayStart = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        harness.TimeSessions.Seed(Session(context, dayStart, dayStart.AddHours(10)));
        var timeline = CreateTimeline(harness);

        var result = await timeline.CalculateBudgetAsync(context, dayStart, dayStart.AddDays(1), weekStart,
            weekStart.AddDays(7));

        Assert.Equal(600, result.DayWorkedMinutes);
        Assert.Equal(0, result.DayRemainingMinutes);
    }

    [Fact]
    public async Task FindAsync_ReturnsNullWhenSessionMissing()
    {
        var harness = new MongoHarness();
        var timeline = CreateTimeline(harness);

        var result = await timeline.FindAsync(Tenant(), ObjectId.GenerateNewId());

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsSessionOwnedByTenant()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var session = Session(context, Now.AddHours(-2), Now.AddHours(-1));
        harness.TimeSessions.Seed(session);
        var timeline = CreateTimeline(harness);

        var result = await timeline.FindAsync(context, session.Id);

        Assert.NotNull(result);
        Assert.Equal(session.Id, result!.Id);
    }

    [Fact]
    public async Task FindAsync_ReturnsNullForSessionOwnedByAnotherUser()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var session = Session(new TenantContext(ObjectId.GenerateNewId(), context.OrganizationId), Now.AddHours(-1),
            Now);
        harness.TimeSessions.Seed(session);
        var timeline = CreateTimeline(harness);

        var result = await timeline.FindAsync(context, session.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ToHistoryAsync_IncludesContractAndProjectDetails()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var project = new ProjectEntity { OrganizationId = context.OrganizationId, Name = "Apollo" };
        var contract = new BillingContractEntity
        {
            OrganizationId = context.OrganizationId,
            ProjectId = project.Id,
            ServiceProvided = "Engineering"
        };
        harness.Projects.Seed(project);
        harness.BillingContracts.Seed(contract);
        var session = Session(context, Now.AddHours(-2), Now.AddHours(-1), contract.Id, project.Id);
        var timeline = CreateTimeline(harness);

        var history = await timeline.ToHistoryAsync(context, session, null);

        Assert.Equal(session.Id.ToString(), history.SessionId);
        Assert.Equal("Apollo", history.ProjectName);
        Assert.Equal("Engineering", history.ServiceProvided);
        Assert.Equal(60, history.ElapsedMinutes);
    }

    [Fact]
    public async Task ToHistoryAsync_MarksSessionLockedWhenInvoiceIsNotDraft()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var session = Session(context, Now.AddHours(-2), Now.AddHours(-1), invoiceId: ObjectId.GenerateNewId());
        var timeline = CreateTimeline(harness);

        var history = await timeline.ToHistoryAsync(context, session, nameof(Status.Paid));

        Assert.False(history.CanEdit);
        Assert.False(history.CanDiscard);
    }

    [Fact]
    public async Task LoadInvoiceStatusAsync_ReturnsNullWhenSessionIsNotInvoiced()
    {
        var harness = new MongoHarness();
        var timeline = CreateTimeline(harness);

        var result = await timeline.LoadInvoiceStatusAsync(Tenant(), null);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadInvoiceStatusAsync_ReturnsStatusForTenantInvoice()
    {
        var harness = new MongoHarness();
        var context = Tenant();
        var invoice = new InvoiceEntity { OrganizationId = context.OrganizationId, Status = nameof(Status.Draft) };
        harness.Invoices.Seed(invoice);
        var timeline = CreateTimeline(harness);

        var result = await timeline.LoadInvoiceStatusAsync(context, invoice.Id);

        Assert.Equal(nameof(Status.Draft), result);
    }

    [Fact]
    public async Task LoadInvoiceStatusAsync_ReturnsNullForInvoiceFromAnotherOrganization()
    {
        var harness = new MongoHarness();
        var invoice = new InvoiceEntity { OrganizationId = ObjectId.GenerateNewId(), Status = nameof(Status.Draft) };
        harness.Invoices.Seed(invoice);
        var timeline = CreateTimeline(harness);

        var result = await timeline.LoadInvoiceStatusAsync(Tenant(), invoice.Id);

        Assert.Null(result);
    }
}