using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Gig;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Test.Gig;

public sealed class GigSessionServiceTests
{
    private static readonly DateTimeOffset NowOffset = new(new DateTime(2026, 8, 12, 18, 0, 0, DateTimeKind.Utc));
    private static readonly DateTime Now = NowOffset.UtcDateTime;

    [Fact]
    public async Task Services_WorkWithAnOrganizationLessTenant()
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Harness.Organizations.Documents);
        Assert.Empty(fixture.Harness.Memberships.Documents);
    }

    [Fact]
    public async Task Services_RejectAnUnresolvedUser()
    {
        var fixture = CreateFixture(TestTenant.Anonymous());

        Assert.Equal(TenantErrors.NotResolved, (await fixture.Sessions.GetCurrentAsync(null)).Error);
        Assert.Equal(TenantErrors.NotResolved, (await fixture.Sessions.ListAsync(10, null)).Error);
        Assert.Equal(TenantErrors.NotResolved,
            (await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null)).Error);
        Assert.Equal(TenantErrors.NotResolved,
            (await fixture.Sessions.DeleteAsync(ObjectId.GenerateNewId().ToString())).Error);
    }

    [Fact]
    public async Task StartAsync_CreatesAnOpenSessionForTheUser()
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, 1000m, "Car"), null);

        Assert.True(result.IsSuccess);
        var session = Assert.Single(fixture.Harness.GigSessions.Documents);
        Assert.Equal(fixture.UserId, session.UserId);
        Assert.True(session.IsOpen);
        Assert.Null(session.EndTime);
        Assert.Equal(Now, session.StartTime);
        Assert.Equal(1000m, session.StartOdometer);
        Assert.NotNull(result.Value.CurrentSession);
        Assert.True(result.Value.CurrentSession.IsOpen);
    }

    [Fact]
    public async Task StartAsync_RejectsASecondOpenSession()
    {
        var fixture = CreateFixture();
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null);

        var result = await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null);

        Assert.Equal(GigErrors.SessionAlreadyOpen, result.Error);
        Assert.Single(fixture.Harness.GigSessions.Documents);
    }

    [Fact]
    public async Task StartAsync_AnotherUsersOpenSessionDoesNotBlock()
    {
        var fixture = CreateFixture();
        fixture.Harness.GigSessions.Seed(new GigSession
        {
            UserId = ObjectId.GenerateNewId(), StartTime = Now.AddHours(-1)
        });

        var result = await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Harness.GigSessions.Documents.Count);
    }

    [Fact]
    public async Task StopAsync_WithNoDetailsEndsTheSessionWithNoIncome()
    {
        var fixture = CreateFixture();
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddHours(-3), null, null), null);

        var result = await fixture.Sessions.StopAsync(new StopGigSessionRequest(null, null, null, null, null), null);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsOpen);
        Assert.Equal(180, result.Value.Minutes);
        Assert.Empty(result.Value.Earnings);
        Assert.Equal(0m, result.Value.GrossEarnings);
        Assert.False(fixture.Harness.GigSessions.Documents.Single().IsOpen);
    }

    [Fact]
    public async Task StopAsync_RecordsEarningsExpensesAndOdometer()
    {
        var fixture = CreateFixture();
        var uber = SeedDefaultSource(fixture, "Uber Eats");
        var doorDash = SeedDefaultSource(fixture, "DoorDash");
        var fuel = SeedDefaultCategory(fixture, "Fuel");
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddHours(-4), 1000m, "Car"), null);

        var result = await fixture.Sessions.StopAsync(
            new StopGigSessionRequest(
                null,
                1040m,
                "Busy lunch",
                [
                    new EarningEntry(uber.Id.ToString(), 55.5m, null),
                    new EarningEntry(doorDash.Id.ToString(), 30m, "tips")
                ],
                [new ExpenseEntry(fuel.Id.ToString(), 12m, null)]),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(85.5m, result.Value.GrossEarnings);
        Assert.Equal(12m, result.Value.TotalExpenses);
        Assert.Equal(73.5m, result.Value.Profit);
        Assert.Equal(40m, result.Value.Miles);
        Assert.Equal("Busy lunch", result.Value.Notes);
        Assert.Equal(2, result.Value.Earnings.Count);
        Assert.Null(result.Value.MileageDeduction);
    }

    [Fact]
    public async Task StopAsync_FailsWhenNoSessionIsRunning()
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.StopAsync(new StopGigSessionRequest(null, null, null, null, null), null);

        Assert.Equal(GigErrors.NoOpenSession, result.Error);
    }

    [Fact]
    public async Task StopAsync_RejectsEndOdometerBelowStartOdometer()
    {
        var fixture = CreateFixture();
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddHours(-1), 500m, null), null);

        var result = await fixture.Sessions.StopAsync(new StopGigSessionRequest(null, 400m, null, null, null), null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
        Assert.True(fixture.Harness.GigSessions.Documents.Single().IsOpen);
    }

    [Fact]
    public async Task StopAsync_RejectsSourcesThatBelongToAnotherUser()
    {
        var fixture = CreateFixture();
        var foreign = new GigSource { UserId = ObjectId.GenerateNewId(), Name = "Secret" };
        fixture.Harness.GigSources.Seed(foreign);
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddHours(-1), null, null), null);

        var result = await fixture.Sessions.StopAsync(
            new StopGigSessionRequest(null, null, null, [new EarningEntry(foreign.Id.ToString(), 5m, null)], null),
            null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task StopAsync_ShowsMileageDeductionOnlyWhenEnabledAndVehicleQualifies()
    {
        var fixture = CreateFixture();
        fixture.Harness.GigSettings.Seed(new GigSettings
        {
            UserId = fixture.UserId, MileageDeductionEnabled = true, MileageRateOverride = 0.5m
        });
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddHours(-2), 0m, "Car"), null);

        var car = await fixture.Sessions.StopAsync(new StopGigSessionRequest(null, 20m, null, null, null), null);
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(Now.AddMinutes(-30), 0m, "Bicycle"), null);
        var bike = await fixture.Sessions.StopAsync(new StopGigSessionRequest(null, 20m, null, null, null), null);

        Assert.Equal(10m, car.Value.MileageDeduction);
        Assert.Null(bike.Value.MileageDeduction);
        Assert.False(bike.Value.IsMileageDeductible);
    }

    [Fact]
    public async Task CreateAsync_RequiresAnEndTime()
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.CreateAsync(
            new GigSessionRequest(Now.AddHours(-3), null, null, null, null, null, null, null), null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateAsync_LogsACompletedSessionAfterTheFact()
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.CreateAsync(
            new GigSessionRequest(Now.AddDays(-2), Now.AddDays(-2).AddHours(3), null, null, null, "Backfilled", null,
                null),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(180, result.Value.Minutes);
        Assert.False(result.Value.IsOpen);
    }

    [Fact]
    public async Task UpdateAsync_EditsAClosedSessionAtAnyTime()
    {
        var fixture = CreateFixture();
        var source = SeedDefaultSource(fixture, "Cash");
        var created = await fixture.Sessions.CreateAsync(
            new GigSessionRequest(Now.AddDays(-40), Now.AddDays(-40).AddHours(2), null, null, null, null, null, null),
            null);

        var result = await fixture.Sessions.UpdateAsync(
            created.Value.Id,
            new GigSessionRequest(
                Now.AddDays(-40),
                Now.AddDays(-40).AddHours(5),
                null,
                null,
                "Bicycle",
                "fixed",
                [new EarningEntry(source.Id.ToString(), 20m, null)],
                null),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(300, result.Value.Minutes);
        Assert.Equal(20m, result.Value.GrossEarnings);
        Assert.Equal("Bicycle", result.Value.VehicleType);
    }

    [Fact]
    public async Task UpdateAsync_ClosedSessionCannotBeReopened()
    {
        var fixture = CreateFixture();
        var created = await fixture.Sessions.CreateAsync(
            new GigSessionRequest(Now.AddDays(-1), Now.AddDays(-1).AddHours(2), null, null, null, null, null, null),
            null);

        var result = await fixture.Sessions.UpdateAsync(
            created.Value.Id,
            new GigSessionRequest(Now.AddDays(-1), null, null, null, null, null, null, null),
            null);

        Assert.True(result.IsFailure);
        Assert.False(fixture.Harness.GigSessions.Documents.Single().IsOpen);
    }

    [Fact]
    public async Task UpdateAsync_AndGetAndDelete_NeverTouchAnotherUsersSession()
    {
        var fixture = CreateFixture();
        var foreign = new GigSession
        {
            UserId = ObjectId.GenerateNewId(), StartTime = Now.AddDays(-1), EndTime = Now.AddDays(-1).AddHours(1)
        };
        fixture.Harness.GigSessions.Seed(foreign);
        var id = foreign.Id.ToString();

        var get = await fixture.Sessions.GetAsync(id, null);
        var update = await fixture.Sessions.UpdateAsync(
            id, new GigSessionRequest(Now.AddDays(-1), Now.AddDays(-1).AddHours(2), null, null, null, null, null, null),
            null);
        var delete = await fixture.Sessions.DeleteAsync(id);

        Assert.Equal(GigErrors.SessionNotFound, get.Error);
        Assert.Equal(GigErrors.SessionNotFound, update.Error);
        Assert.Equal(GigErrors.SessionNotFound, delete.Error);
        Assert.Single(fixture.Harness.GigSessions.Documents);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheCallersSession()
    {
        var fixture = CreateFixture();
        var created = await fixture.Sessions.CreateAsync(
            new GigSessionRequest(Now.AddDays(-1), Now.AddDays(-1).AddHours(2), null, null, null, null, null, null),
            null);

        var result = await fixture.Sessions.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Harness.GigSessions.Documents);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheCallersSessions()
    {
        var fixture = CreateFixture();
        fixture.Harness.GigSessions.Seed(
            new GigSession
                { UserId = fixture.UserId, StartTime = Now.AddDays(-1), EndTime = Now.AddDays(-1).AddHours(1) },
            new GigSession
            {
                UserId = ObjectId.GenerateNewId(), StartTime = Now.AddDays(-1), EndTime = Now.AddDays(-1).AddHours(1)
            });

        var result = await fixture.Sessions.ListAsync(10, null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ListAsync_RejectsOutOfRangeTake(int take)
    {
        var fixture = CreateFixture();

        var result = await fixture.Sessions.ListAsync(take, null);

        Assert.Equal(GigErrors.TakeOutOfRange, result.Error);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsTheRunningSessionOrNone()
    {
        var fixture = CreateFixture();
        var none = await fixture.Sessions.GetCurrentAsync(null);
        await fixture.Sessions.StartAsync(new StartGigSessionRequest(null, null, null), null);

        var running = await fixture.Sessions.GetCurrentAsync(null);

        Assert.Null(none.Value.CurrentSession);
        Assert.NotNull(running.Value.CurrentSession);
        Assert.Equal(Now, running.Value.ServerTime);
    }

    [Fact]
    public async Task ReportAsync_ComputesFromStoredSessionsOnRead()
    {
        var fixture = CreateFixture();
        var source = SeedDefaultSource(fixture, "Cash");
        await fixture.Sessions.CreateAsync(
            new GigSessionRequest(
                Now.AddHours(-5), Now.AddHours(-3), null, null, null, null,
                [new EarningEntry(source.Id.ToString(), 60m, null)], null),
            null);

        var report = await fixture.Reports.GetAsync(4, null);

        Assert.True(report.IsSuccess);
        Assert.Equal(60m, report.Value.ThisWeek.Gross);
        Assert.Equal(30m, report.Value.ThisWeek.GrossHourlyRate);
        Assert.Equal(4, report.Value.Weeks.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public async Task ReportAsync_RejectsOutOfRangeWeeks(int weeks)
    {
        var fixture = CreateFixture();

        var result = await fixture.Reports.GetAsync(weeks, null);

        Assert.Equal(GigErrors.WeeksOutOfRange, result.Error);
    }

    private static GigSource SeedDefaultSource(Fixture fixture, string name)
    {
        var source = new GigSource { Name = name, IsDefault = true };
        fixture.Harness.GigSources.Seed(source);
        return source;
    }

    private static GigExpenseCategory SeedDefaultCategory(Fixture fixture, string name)
    {
        var category = new GigExpenseCategory { Name = name, IsDefault = true };
        fixture.Harness.GigExpenseCategories.Seed(category);
        return category;
    }

    private static Fixture CreateFixture(CurrentTenant? tenant = null)
    {
        var userId = ObjectId.GenerateNewId();
        var harness = new MongoHarness();
        var timeProvider = new StaticTimeProvider(NowOffset);
        var currentTenant = tenant ?? TestTenant.Create(userId);
        var settings = new GigSettingsService(harness.Database, currentTenant, timeProvider);
        var sessions = new GigSessionService(harness.Database, currentTenant, settings, timeProvider);
        var reports = new GigReportService(harness.Database, currentTenant, settings, timeProvider);
        return new Fixture(userId, harness, sessions, reports);
    }

    private sealed record Fixture(
        ObjectId UserId,
        MongoHarness Harness,
        GigSessionService Sessions,
        GigReportService Reports);
}