using MongoDB.Bson;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Api.Service.Timekeeping;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.Membership;
using StartRequest = SartainStudios.Schema.WorkSession.StartRequest;
using StopRequest = SartainStudios.Schema.WorkSession.StopRequest;
using UpdateRequest = SartainStudios.Schema.WorkSession.UpdateRequest;
using WorkSessionErrors = SartainStudios.Schema.WorkSession.WorkSessionErrors;
using MembershipEntity = SartainStudios.Schema.DatabaseEntity.Membership;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class WorkSessionServiceTests
{
    private static WorkSessionService CreateService(
        MongoHarness harness,
        CurrentTenant? tenant = null,
        TimeProvider? timeProvider = null)
    {
        var currentTenant = tenant ?? TestTenant.Anonymous();
        var access = new Access(harness.Database, currentTenant);
        var tracker = new Tracker(harness.Database);
        var lookup = new Lookup(harness.Database);
        var timeline = new Timeline(harness.Database, lookup);
        var draft = new Draft(harness.Database);
        var editing = new Editing(harness.Database, harness.Client, draft);
        return new WorkSessionService(access, tracker, timeline, editing, draft,
            timeProvider ?? new StaticTimeProvider(DateTimeOffset.UtcNow));
    }

    private static (WorkSessionService Service, ObjectId UserId, ObjectId OrganizationId) CreateAuthenticatedService(
        MongoHarness harness,
        TimeProvider? timeProvider = null)
    {
        var userId = ObjectId.GenerateNewId();
        var organizationId = ObjectId.GenerateNewId();
        harness.Memberships.Seed(new MembershipEntity
        {
            UserId = userId,
            OrganizationId = organizationId,
            Status = nameof(RoleStatus.Active)
        });
        var tenant = TestTenant.Create(userId, organizationId);
        return (CreateService(harness, tenant, timeProvider), userId, organizationId);
    }

    [Fact]
    public async Task ListAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.ListAsync(null, 10);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task ListAsync_ReturnsTakeOutOfRangeWhenTakeIsZero()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.ListAsync(null, 0);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.TakeOutOfRange, result.Error);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyListWhenNoSessions()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.ListAsync(null, 10);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListAsync_ReturnsInvalidContractIdWhenBadContractId()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.ListAsync("not-a-valid-id", 10);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidContractId, result.Error);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetProgressAsync(null);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetTimeBudgetAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var dayStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = new DateTime(2026, 1, 1, 23, 59, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2025, 12, 29, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = new DateTime(2026, 1, 4, 23, 59, 0, DateTimeKind.Utc);

        var result = await service.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetTimeBudgetAsync_ReturnsBoundariesMustBeUtcWhenNotUtc()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var dayStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var dayEnd = new DateTime(2026, 1, 1, 23, 59, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2025, 12, 29, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = new DateTime(2026, 1, 4, 23, 59, 0, DateTimeKind.Utc);

        var result = await service.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.BoundariesMustBeUtc, result.Error);
    }

    [Fact]
    public async Task GetTimeBudgetAsync_ReturnsDayEndBeforeStartWhenInvalid()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var dayStart = new DateTime(2026, 1, 1, 23, 59, 0, DateTimeKind.Utc);
        var dayEnd = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2025, 12, 29, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = new DateTime(2026, 1, 4, 23, 59, 0, DateTimeKind.Utc);

        var result = await service.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.DayEndBeforeStart, result.Error);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.GetCurrentAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsNotRunningWhenNoSession()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.GetCurrentAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasRunningSession);
    }

    [Fact]
    public async Task GetAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.GetAsync("not-a-valid-id");

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var id = ObjectId.GenerateNewId().ToString();

        var result = await service.GetAsync(id);

        Assert.True(result.IsFailure);
        Assert.Equal("WorkSession.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task StartAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);
        var request = new StartRequest(ObjectId.GenerateNewId().ToString(), null);

        var result = await service.StartAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task StartAsync_ReturnsInvalidContractIdWhenBadId()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var request = new StartRequest("bad-id", null);

        var result = await service.StartAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidContractId, result.Error);
    }

    [Fact]
    public async Task StartAsync_ReturnsContractNotFoundWhenContractMissing()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var (service, _, _) = CreateAuthenticatedService(harness, new StaticTimeProvider(now));
        var request = new StartRequest(ObjectId.GenerateNewId().ToString(), null);

        var result = await service.StartAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.ContractNotFound, result.Error);
    }

    [Fact]
    public async Task StartAsync_ReturnsTimerAlreadyRunningWhenSessionActive()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var (service, userId, organizationId) = CreateAuthenticatedService(harness, new StaticTimeProvider(now));
        harness.TimeSessions.Seed(new WorkSessionEntity
        {
            OrganizationId = organizationId,
            UserId = userId,
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = now.UtcDateTime.AddHours(-1)
        });
        var request = new StartRequest(ObjectId.GenerateNewId().ToString(), null);

        var result = await service.StartAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.TimerAlreadyRunning, result.Error);
    }

    [Fact]
    public async Task StopAsync_ReturnsTenantErrorWhenAnonymous()
    {
        var harness = new MongoHarness();
        var service = CreateService(harness);

        var result = await service.StopAsync(new StopRequest(null));

        Assert.True(result.IsFailure);
        Assert.Equal(TenantErrors.NotResolved, result.Error);
    }

    [Fact]
    public async Task StopAsync_ReturnsNoRunningTimerWhenNotStarted()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.StopAsync(new StopRequest(null));

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.NoRunningTimer, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var now = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        var request = new UpdateRequest(now.AddHours(-2), now.AddHours(-1));

        var result = await service.UpdateAsync("not-valid", request);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);
        var now = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        var request = new UpdateRequest(now.AddHours(-2), now.AddHours(-1));

        var result = await service.UpdateAsync(ObjectId.GenerateNewId().ToString(), request);

        Assert.True(result.IsFailure);
        Assert.Equal("WorkSession.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DiscardAsync_ReturnsInvalidIdWhenIdMalformed()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.DiscardAsync("not-valid");

        Assert.True(result.IsFailure);
        Assert.Equal(WorkSessionErrors.InvalidId, result.Error);
    }

    [Fact]
    public async Task DiscardAsync_ReturnsNotFoundWhenMissing()
    {
        var harness = new MongoHarness();
        var (service, _, _) = CreateAuthenticatedService(harness);

        var result = await service.DiscardAsync(ObjectId.GenerateNewId().ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("WorkSession.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DiscardAsync_DeletesUnbilledSession()
    {
        var harness = new MongoHarness();
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var (service, userId, organizationId) = CreateAuthenticatedService(harness, new StaticTimeProvider(now));
        var sessionId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(new WorkSessionEntity
        {
            Id = sessionId,
            OrganizationId = organizationId,
            UserId = userId,
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = now.UtcDateTime.AddHours(-2),
            EndTime = now.UtcDateTime.AddHours(-1)
        });

        var result = await service.DiscardAsync(sessionId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.TimeSessions.Documents);
    }
}