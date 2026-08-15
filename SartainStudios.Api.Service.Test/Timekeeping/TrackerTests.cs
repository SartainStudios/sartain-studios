using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Api.Service.Timekeeping;
using BillingContractEntity = SartainStudios.Schema.DatabaseEntity.BillingContract;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class TrackerTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    private static WorkSessionEntity Session(
        ObjectId organizationId,
        ObjectId userId,
        DateTime startTime,
        DateTime? endTime = null,
        ObjectId? sessionId = null,
        ObjectId? contractId = null,
        ObjectId? projectId = null)
    {
        return new WorkSessionEntity
        {
            Id = sessionId ?? ObjectId.GenerateNewId(),
            OrganizationId = organizationId,
            UserId = userId,
            ContractId = contractId ?? ObjectId.GenerateNewId(),
            ProjectId = projectId ?? ObjectId.GenerateNewId(),
            StartTime = startTime,
            EndTime = endTime
        };
    }

    [Fact]
    public async Task LoadCurrentAsync_ReturnsNullWhenNoSessionsExist()
    {
        var harness = new MongoHarness();
        var tracker = new Tracker(harness.Database);

        var result = await tracker.LoadCurrentAsync(ObjectId.GenerateNewId(), ObjectId.GenerateNewId());

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadCurrentAsync_ReturnsRunningSession()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var running = Session(organizationId, userId, Now.AddHours(-1));
        harness.TimeSessions.Seed(running);
        var tracker = new Tracker(harness.Database);

        var result = await tracker.LoadCurrentAsync(organizationId, userId);

        Assert.NotNull(result);
        Assert.Equal(running.Id, result!.Id);
    }

    [Fact]
    public async Task LoadCurrentAsync_IgnoresCompletedSessions()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-3), Now.AddHours(-2)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.LoadCurrentAsync(organizationId, userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadCurrentAsync_IgnoresSessionsOwnedByAnotherUser()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, ObjectId.GenerateNewId(), Now.AddHours(-1)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.LoadCurrentAsync(organizationId, ObjectId.GenerateNewId());

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadCurrentAsync_IgnoresSessionsOwnedByAnotherOrganization()
    {
        var harness = new MongoHarness();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(ObjectId.GenerateNewId(), userId, Now.AddHours(-1)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.LoadCurrentAsync(ObjectId.GenerateNewId(), userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsFalseWhenNoSessionsExist()
    {
        var harness = new MongoHarness();
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(),
            Now.AddHours(-1), Now);

        Assert.False(result);
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsTrueWhenCompletedSessionOverlaps()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-2), Now.AddHours(-1)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, userId, Now.AddMinutes(-90), Now.AddMinutes(-30));

        Assert.True(result);
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsFalseWhenSessionEndsBeforeInterval()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-5), Now.AddHours(-4)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, userId, Now.AddHours(-2), Now.AddHours(-1));

        Assert.False(result);
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsFalseWhenSessionStartsAfterInterval()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-1), Now));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, userId, Now.AddHours(-5), Now.AddHours(-4));

        Assert.False(result);
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsTrueWhenRunningSessionStartedBeforeInterval()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-6)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, userId, Now.AddHours(-2), Now.AddHours(-1));

        Assert.True(result);
    }

    [Fact]
    public async Task HasOverlapAsync_IgnoresSessionsOwnedByAnotherUser()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, ObjectId.GenerateNewId(), Now.AddHours(-2),
            Now.AddHours(-1)));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, ObjectId.GenerateNewId(), Now.AddHours(-2), Now);

        Assert.False(result);
    }

    [Fact]
    public async Task HasOverlapAsync_IgnoresExcludedSession()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var session = Session(organizationId, userId, Now.AddHours(-2), Now.AddHours(-1));
        harness.TimeSessions.Seed(session);
        var tracker = new Tracker(harness.Database);

        var result = await tracker.HasOverlapAsync(organizationId, userId, Now.AddHours(-2), Now, session.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task TryStartAsync_InsertsSessionAndReturnsTrue()
    {
        var harness = new MongoHarness();
        var session = Session(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), Now);
        var tracker = new Tracker(harness.Database);

        var result = await tracker.TryStartAsync(session);

        Assert.True(result);
        Assert.Same(session, Assert.Single(harness.TimeSessions.Inserted));
    }

    [Fact]
    public async Task TryStartAsync_ReturnsFalseWhenDuplicateKeyConflict()
    {
        var harness = new MongoHarness();
        harness.TimeSessions.WriteFailure = MongoErrors.DuplicateKey();
        var tracker = new Tracker(harness.Database);

        var result = await tracker.TryStartAsync(Session(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), Now));

        Assert.False(result);
        Assert.Empty(harness.TimeSessions.Documents);
    }

    [Fact]
    public async Task TryStartAsync_RethrowsWriteFailuresThatAreNotDuplicateKeys()
    {
        var harness = new MongoHarness();
        harness.TimeSessions.WriteFailure = MongoErrors.Uncategorized();
        var tracker = new Tracker(harness.Database);

        await Assert.ThrowsAsync<MongoWriteException>(() =>
            tracker.TryStartAsync(Session(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), Now)));
    }

    [Fact]
    public async Task TryStopAsync_SetsEndTimeAndReturnsTrue()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-1), sessionId: sessionId));
        var session = Session(organizationId, userId, Now.AddHours(-1), sessionId: sessionId);
        var tracker = new Tracker(harness.Database);

        var result = await tracker.TryStopAsync(session, Now);

        Assert.True(result);
        Assert.Equal(Now, session.EndTime);
        Assert.NotEqual(default, session.UpdatedAt);
        Assert.Same(session, Assert.Single(harness.TimeSessions.Replaced));
    }

    [Fact]
    public async Task TryStopAsync_ReturnsFalseWhenSessionAlreadyStopped()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var sessionId = ObjectId.GenerateNewId();
        harness.TimeSessions.Seed(Session(organizationId, userId, Now.AddHours(-2), Now.AddHours(-1), sessionId));
        var session = Session(organizationId, userId, Now.AddHours(-2), sessionId: sessionId);
        var tracker = new Tracker(harness.Database);

        var result = await tracker.TryStopAsync(session, Now);

        Assert.False(result);
        Assert.Empty(harness.TimeSessions.Replaced);
    }

    [Fact]
    public async Task TryStopAsync_ReturnsFalseWhenSessionMissing()
    {
        var harness = new MongoHarness();
        var session = Session(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), Now.AddHours(-1));
        var tracker = new Tracker(harness.Database);

        var result = await tracker.TryStopAsync(session, Now);

        Assert.False(result);
    }

    [Fact]
    public async Task LoadStartTargetsAsync_ReturnsNullsWhenContractMissing()
    {
        var harness = new MongoHarness();
        var tracker = new Tracker(harness.Database);

        var (contract, project) =
            await tracker.LoadStartTargetsAsync(ObjectId.GenerateNewId(), ObjectId.GenerateNewId());

        Assert.Null(contract);
        Assert.Null(project);
    }

    [Fact]
    public async Task LoadStartTargetsAsync_ReturnsNullsWhenContractBelongsToAnotherOrganization()
    {
        var harness = new MongoHarness();
        var contractEntity = new BillingContractEntity { OrganizationId = ObjectId.GenerateNewId() };
        harness.BillingContracts.Seed(contractEntity);
        var tracker = new Tracker(harness.Database);

        var (contract, project) = await tracker.LoadStartTargetsAsync(ObjectId.GenerateNewId(), contractEntity.Id);

        Assert.Null(contract);
        Assert.Null(project);
    }

    [Fact]
    public async Task LoadStartTargetsAsync_ReturnsContractAndProject()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var projectEntity = new ProjectEntity { OrganizationId = organizationId, Name = "Apollo" };
        var contractEntity = new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = projectEntity.Id,
            ServiceProvided = "Engineering"
        };
        harness.Projects.Seed(projectEntity);
        harness.BillingContracts.Seed(contractEntity);
        var tracker = new Tracker(harness.Database);

        var (contract, project) = await tracker.LoadStartTargetsAsync(organizationId, contractEntity.Id);

        Assert.NotNull(contract);
        Assert.Equal(contractEntity.Id, contract!.Id);
        Assert.NotNull(project);
        Assert.Equal("Apollo", project!.Name);
    }

    [Fact]
    public async Task LoadStartTargetsAsync_ReturnsContractWithoutProjectWhenProjectMissing()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var contractEntity = new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = ObjectId.GenerateNewId()
        };
        harness.BillingContracts.Seed(contractEntity);
        var tracker = new Tracker(harness.Database);

        var (contract, project) = await tracker.LoadStartTargetsAsync(organizationId, contractEntity.Id);

        Assert.NotNull(contract);
        Assert.Null(project);
    }

    [Fact]
    public async Task BuildStateAsync_ReturnsIdleStateWhenNoRunningSession()
    {
        var harness = new MongoHarness();
        var tracker = new Tracker(harness.Database);

        var state = await tracker.BuildStateAsync(ObjectId.GenerateNewId(), null);

        Assert.False(state.HasRunningSession);
        Assert.Null(state.CurrentSession);
    }

    [Fact]
    public async Task BuildStateAsync_ReturnsRunningStateWithContractAndProjectDetails()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var projectEntity = new ProjectEntity { OrganizationId = organizationId, Name = "Apollo" };
        var contractEntity = new BillingContractEntity
        {
            OrganizationId = organizationId,
            ProjectId = projectEntity.Id,
            ServiceProvided = "Engineering"
        };
        harness.Projects.Seed(projectEntity);
        harness.BillingContracts.Seed(contractEntity);
        var session = Session(organizationId, userId, Now.AddHours(-1), contractId: contractEntity.Id,
            projectId: projectEntity.Id);

        var tracker = new Tracker(harness.Database);

        var state = await tracker.BuildStateAsync(organizationId, session);

        Assert.True(state.HasRunningSession);
        Assert.NotNull(state.CurrentSession);
        Assert.Equal(session.Id.ToString(), state.CurrentSession!.SessionId);
        Assert.Equal("Apollo", state.CurrentSession.ProjectName);
        Assert.Equal("Engineering", state.CurrentSession.ServiceProvided);
        Assert.Null(state.CurrentSession.EndTime);
    }

    [Fact]
    public async Task BuildStateAsync_FallsBackToEmptyNamesWhenRelatedDocumentsMissing()
    {
        var harness = new MongoHarness();
        var organizationId = ObjectId.GenerateNewId();
        var session = Session(organizationId, ObjectId.GenerateNewId(), Now.AddHours(-1));
        var tracker = new Tracker(harness.Database);

        var state = await tracker.BuildStateAsync(organizationId, session);

        Assert.True(state.HasRunningSession);
        Assert.Equal(string.Empty, state.CurrentSession!.ProjectName);
        Assert.Equal(string.Empty, state.CurrentSession.ServiceProvided);
    }
}