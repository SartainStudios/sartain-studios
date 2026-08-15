using MongoDB.Bson;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Authentication;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.Api;
using SartainStudios.Schema.WorkSession;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using ProjectStatus = SartainStudios.Schema.Project.Status;
using Status = SartainStudios.Schema.Invoice.Status;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Timekeeping;

public sealed class WorkSessionService(
    Access access,
    Tracker tracker,
    Timeline timeline,
    Editing editing,
    Draft draftInvoice,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<History>>> ListAsync(
        string? contractId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (take is < WorkSessionErrors.MinimumTake or > WorkSessionErrors.MaximumTake)
            return WorkSessionErrors.TakeOutOfRange;
        var parsedContractId = Timing.ParseOptionalContractId(contractId);
        if (parsedContractId.IsFailure)
            return parsedContractId.Error;
        var history = await timeline.ListAsync(context.Value, parsedContractId.Value, take);
        return Result.Success(history);
    }

    public async Task<Result<IReadOnlyList<Progress>>> GetProgressAsync(
        string? contractId,
        CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var parsedContractId = Timing.ParseOptionalContractId(contractId);
        if (parsedContractId.IsFailure)
            return parsedContractId.Error;
        var progress = await timeline.ProgressAsync(context.Value, parsedContractId.Value);
        return Result.Success(progress);
    }

    public async Task<Result<TimeBudget>> GetTimeBudgetAsync(
        DateTime dayStart,
        DateTime dayEnd,
        DateTime weekStart,
        DateTime weekEnd,
        CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (dayStart.Kind != DateTimeKind.Utc || dayEnd.Kind != DateTimeKind.Utc ||
            weekStart.Kind != DateTimeKind.Utc || weekEnd.Kind != DateTimeKind.Utc)
            return WorkSessionErrors.BoundariesMustBeUtc;
        if (dayEnd <= dayStart)
            return WorkSessionErrors.DayEndBeforeStart;
        if (weekEnd <= weekStart)
            return WorkSessionErrors.WeekEndBeforeStart;
        if (dayStart < weekStart || dayEnd > weekEnd)
            return WorkSessionErrors.DayOutsideWeek;
        return await timeline.CalculateBudgetAsync(context.Value, dayStart, dayEnd, weekStart, weekEnd);
    }

    public async Task<Result<State>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var currentSession = await tracker.LoadCurrentAsync(context.Value.OrganizationId, context.Value.UserId);
        return await tracker.BuildStateAsync(context.Value.OrganizationId, currentSession);
    }

    public async Task<Result<History>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var invoiceStatus = await timeline.LoadInvoiceStatusAsync(loaded.Value.Context, loaded.Value.Session.InvoiceId);
        return await timeline.ToHistoryAsync(loaded.Value.Context, loaded.Value.Session, invoiceStatus);
    }

    public async Task<Result<State>> StartAsync(StartRequest request, CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var contractId = Timing.ParseContractId(request.ContractId);
        if (contractId.IsFailure)
            return contractId.Error;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var startTime = Timing.NormalizeStartTime(request.StartTime, now);
        if (startTime.IsFailure)
            return startTime.Error;
        var currentSession = await tracker.LoadCurrentAsync(context.Value.OrganizationId, context.Value.UserId);
        if (currentSession is not null)
            return WorkSessionErrors.TimerAlreadyRunning;
        var (contract, project) = await tracker.LoadStartTargetsAsync(context.Value.OrganizationId, contractId.Value);
        if (contract is null)
            return WorkSessionErrors.ContractNotFound;
        if (!contract.IsActive)
            return WorkSessionErrors.ContractInactive;
        if (project is null)
            return WorkSessionErrors.ProjectNotFound;
        if (!string.Equals(project.Status, nameof(ProjectStatus.Active), StringComparison.OrdinalIgnoreCase))
            return WorkSessionErrors.ProjectArchived;
        if (startTime.Value > now)
            return WorkSessionErrors.StartTimeInFuture;
        if (await tracker.HasOverlapAsync(context.Value.OrganizationId, context.Value.UserId, startTime.Value, now))
            return WorkSessionErrors.OverlapConflict;
        var session = new WorkSessionEntity
        {
            OrganizationId = context.Value.OrganizationId,
            UserId = context.Value.UserId,
            ContractId = contract.Id,
            ProjectId = contract.ProjectId,
            StartTime = startTime.Value,
            EndTime = null,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (!await tracker.TryStartAsync(session))
            return WorkSessionErrors.StartConflict;
        return await tracker.BuildStateAsync(context.Value.OrganizationId, session);
    }

    public async Task<Result<State>> StopAsync(StopRequest request, CancellationToken cancellationToken = default)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        var currentSession = await tracker.LoadCurrentAsync(context.Value.OrganizationId, context.Value.UserId);
        if (currentSession is null)
            return WorkSessionErrors.NoRunningTimer;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var endTime = Timing.NormalizeEndTime(request.EndTime, currentSession.StartTime, now);
        if (endTime.IsFailure)
            return endTime.Error;
        if (await tracker.HasOverlapAsync(context.Value.OrganizationId, context.Value.UserId,
                currentSession.StartTime, endTime.Value, currentSession.Id))
            return WorkSessionErrors.OverlapConflict;
        if (!await tracker.TryStopAsync(currentSession, endTime.Value))
            return WorkSessionErrors.StopConflict;
        return await tracker.BuildStateAsync(context.Value.OrganizationId, null);
    }

    public async Task<Result<History>> UpdateAsync(
        string id,
        UpdateRequest request,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var (context, session) = loaded.Value;
        InvoiceEntity? invoice = null;
        if (session.InvoiceId.HasValue)
        {
            invoice = await draftInvoice.LoadAsync(context.OrganizationId, session.InvoiceId.Value);
            if (invoice is null)
                return WorkSessionErrors.NotEditableBilled;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var range = Timing.NormalizeRange(request.StartTime, request.EndTime, now);
        if (range.IsFailure)
            return range.Error;
        var (startTime, endTime) = range.Value;
        var overlapEndTime = endTime ?? now;
        if (await tracker.HasOverlapAsync(context.OrganizationId, context.UserId, startTime, overlapEndTime,
                session.Id))
            return WorkSessionErrors.OverlapConflict;
        session.StartTime = startTime;
        session.EndTime = endTime;
        session.UpdatedAt = now;
        var saved = invoice is not null
            ? await editing.TryReplaceOnDraftInvoiceAsync(session, invoice, userTimeZone)
            : await editing.TryReplaceUnbilledAsync(session);
        if (!saved)
            return WorkSessionErrors.UpdateConflict;
        return await timeline.ToHistoryAsync(context, session, invoice is null ? null : nameof(Status.Draft));
    }

    public async Task<Result> DiscardAsync(
        string id,
        string userTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userTimeZoneId);
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
            return loaded.Error;
        var (context, session) = loaded.Value;
        if (!session.InvoiceId.HasValue)
        {
            await editing.DeleteUnbilledAsync(session);
            return Result.Success();
        }

        var invoice = await draftInvoice.LoadAsync(context.OrganizationId, session.InvoiceId.Value);
        if (invoice is null)
            return WorkSessionErrors.NotDiscardableBilled;
        return await editing.TryDiscardFromDraftInvoiceAsync(session, invoice, userTimeZone)
            ? Result.Success()
            : WorkSessionErrors.DiscardConflict;
    }

    private async Task<Result<LoadedSession>> LoadAsync(string id, CancellationToken cancellationToken)
    {
        var context = await access.LoadContextAsync();
        if (context is null)
            return TenantErrors.NotResolved;
        if (!ObjectId.TryParse(id, out var sessionId))
            return WorkSessionErrors.InvalidId;
        var session = await timeline.FindAsync(context.Value, sessionId);
        return session is null
            ? WorkSessionErrors.NotFound(id)
            : new LoadedSession(context.Value, session);
    }

    private sealed record LoadedSession(TenantContext Context, WorkSessionEntity Session);
}