using MongoDB.Bson;
using SartainStudios.Api.Service.Timekeeping;
using SartainStudios.Schema.DatabaseEntity;
using ProjectEntity = SartainStudios.Schema.DatabaseEntity.Project;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Test.Timekeeping;

public sealed class PresentationTests
{
    [Fact]
    public void ToHistory_SetsFieldsCorrectlyForCompletedSession()
    {
        var sessionId = ObjectId.GenerateNewId();
        var session = new WorkSessionEntity
        {
            Id = sessionId,
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var contract = new BillingContract { ServiceProvided = "Dev Work" };
        var project = new ProjectEntity { Name = "My Project" };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var history = Presentation.ToHistory(session, contract, project, null, now);

        Assert.Equal(sessionId.ToString(), history.SessionId);
        Assert.Equal("My Project", history.ProjectName);
        Assert.Equal("Dev Work", history.ServiceProvided);
        Assert.Equal(60, history.ElapsedMinutes);
        Assert.False(history.IsRunning);
        Assert.True(history.CanEdit);
        Assert.True(history.CanDiscard);
    }

    [Fact]
    public void ToHistory_IsRunningWhenNoEndTime()
    {
        var session = new WorkSessionEntity
        {
            Id = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        var now = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var history = Presentation.ToHistory(session, null, null, null, now);

        Assert.True(history.IsRunning);
        Assert.Equal(60, history.ElapsedMinutes);
        Assert.Equal(string.Empty, history.ProjectName);
        Assert.Equal(string.Empty, history.ServiceProvided);
    }

    [Fact]
    public void ToHistory_CanEditAndDiscardFalseWhenBilledNonDraft()
    {
        var session = new WorkSessionEntity
        {
            Id = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            InvoiceId = ObjectId.GenerateNewId()
        };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var history = Presentation.ToHistory(session, null, null, "Sent", now);

        Assert.False(history.CanEdit);
        Assert.False(history.CanDiscard);
    }

    [Fact]
    public void ToState_ReturnsNotRunningWhenSessionIsNull()
    {
        var state = Presentation.ToState(null, null, null);
        Assert.False(state.HasRunningSession);
        Assert.Null(state.CurrentSession);
    }

    [Fact]
    public void ToState_ReturnsRunningWithSessionWhenProvided()
    {
        var session = new WorkSessionEntity
        {
            Id = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        var state = Presentation.ToState(session, null, null);

        Assert.True(state.HasRunningSession);
        Assert.NotNull(state.CurrentSession);
        Assert.Equal(session.Id.ToString(), state.CurrentSession.SessionId);
    }
}