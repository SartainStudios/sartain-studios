using MongoDB.Bson;
using SartainStudios.Api.Schema.Invoice;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;

namespace SartainStudios.Api.Service.Invoice;

public static class Selection
{
    public static Result<SelectionRequest> ValidateRequest(
        string? contractId,
        IReadOnlyList<string>? sessionIds,
        DateTime dueDate)
    {
        var errors = new List<(string Field, string Message)>();
        if (!ObjectId.TryParse(contractId, out var parsedContractId))
            errors.Add((InvoiceErrors.ContractIdField, InvoiceErrors.ContractIdInvalid));
        var parsedSessionIds = ParseSessionIds(sessionIds, errors);
        ValidateDueDate(dueDate, errors);
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return new SelectionRequest(parsedContractId, parsedSessionIds);
    }

    public static Result<IReadOnlyList<ObjectId>> ValidateRequest(
        IReadOnlyList<string>? sessionIds,
        DateTime dueDate)
    {
        var errors = new List<(string Field, string Message)>();
        var parsedSessionIds = ParseSessionIds(sessionIds, errors);
        ValidateDueDate(dueDate, errors);
        if (errors.Count > 0)
            return ValidationError.FromErrors([.. errors]);
        return Result.Success(parsedSessionIds);
    }

    public static Result Validate(
        IReadOnlyList<WorkSession> sessions,
        IReadOnlyList<ObjectId> requestedSessionIds,
        BillingContract contract,
        ObjectId? invoiceId = null)
    {
        if (sessions.Count != requestedSessionIds.Count)
            return InvoiceErrors.SessionsUnavailable;
        if (sessions.Any(session => session.EndTime is null))
            return InvoiceErrors.SessionRunning;
        if (sessions.Any(session => session.InvoiceId.HasValue && session.InvoiceId != invoiceId))
            return invoiceId is null
                ? InvoiceErrors.SessionsAlreadyBilled
                : InvoiceErrors.SessionsBilledElsewhere;
        if (sessions.Any(session => session.ProjectId != contract.ProjectId))
            return InvoiceErrors.SessionsOutsideContract;
        if (Totals.HasOverlappingSessions(sessions))
            return InvoiceErrors.SessionsOverlap;
        return Result.Success();
    }

    private static IReadOnlyList<ObjectId> ParseSessionIds(
        IReadOnlyList<string>? sessionIds,
        List<(string Field, string Message)> errors)
    {
        if (sessionIds is null || sessionIds.Count == 0)
        {
            errors.Add((InvoiceErrors.SessionIdsField, InvoiceErrors.SessionIdsRequired));
            return [];
        }

        var parsed = new List<ObjectId>(sessionIds.Count);
        foreach (var sessionId in sessionIds)
        {
            if (!ObjectId.TryParse(sessionId, out var parsedSessionId))
            {
                errors.Add((InvoiceErrors.SessionIdsField, InvoiceErrors.SessionIdsInvalid));
                return [];
            }

            parsed.Add(parsedSessionId);
        }

        if (parsed.Distinct().Count() != parsed.Count)
        {
            errors.Add((InvoiceErrors.SessionIdsField, InvoiceErrors.SessionIdsNotUnique));
            return [];
        }

        return parsed;
    }

    private static void ValidateDueDate(DateTime dueDate, List<(string Field, string Message)> errors)
    {
        if (dueDate.Kind != DateTimeKind.Utc)
            errors.Add((InvoiceErrors.DueDateField, InvoiceErrors.DueDateNotUtc));
    }
}