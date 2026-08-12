using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class SelectionTests
{
    private static BillingContract Contract(ObjectId projectId)
    {
        return new BillingContract { ProjectId = projectId, HourlyRate = 50m };
    }

    [Fact]
    public void ValidateRequest_ReturnsFailureWhenContractIdInvalid()
    {
        var result = Selection.ValidateRequest("not-an-id", ["someSessionId"], DateTime.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateRequest_ReturnsFailureWhenDueDateNotUtc()
    {
        var contractId = ObjectId.GenerateNewId().ToString();
        var sessionId = ObjectId.GenerateNewId().ToString();

        var result = Selection.ValidateRequest(contractId, [sessionId], DateTime.Now);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateRequest_ReturnsSuccessWhenValid()
    {
        var contractId = ObjectId.GenerateNewId().ToString();
        var sessionId = ObjectId.GenerateNewId().ToString();

        var result = Selection.ValidateRequest(contractId, [sessionId], DateTime.UtcNow);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateRequestWithoutContract_ReturnsFailureWhenSessionIdsMissing()
    {
        var result = Selection.ValidateRequest(null, DateTime.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_ReturnsSessionsUnavailableWhenCountsMismatch()
    {
        var projectId = ObjectId.GenerateNewId();
        var contract = Contract(projectId);

        var result = Selection.Validate([], [ObjectId.GenerateNewId()], contract);

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.SessionsUnavailable, result.Error);
    }

    [Fact]
    public void Validate_ReturnsSessionRunningWhenSessionHasNoEndTime()
    {
        var projectId = ObjectId.GenerateNewId();
        var contract = Contract(projectId);
        var session = new WorkSession { ProjectId = projectId, StartTime = DateTime.UtcNow, EndTime = null };

        var result = Selection.Validate([session], [session.Id], contract);

        Assert.True(result.IsFailure);
        Assert.Equal(InvoiceErrors.SessionRunning, result.Error);
    }

    [Fact]
    public void Validate_ReturnsSuccessWhenSessionsValid()
    {
        var projectId = ObjectId.GenerateNewId();
        var contract = Contract(projectId);
        var session = new WorkSession
        {
            ProjectId = projectId,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1)
        };

        var result = Selection.Validate([session], [session.Id], contract);

        Assert.True(result.IsSuccess);
    }
}