using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Authentication;
using SartainStudios.Api.Service.Data;
using History = SartainStudios.Schema.WorkSession.History;
using Progress = SartainStudios.Schema.WorkSession.Progress;
using TimeBudget = SartainStudios.Schema.WorkSession.TimeBudget;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Timekeeping;

public sealed class Timeline(Database database, Lookup lookup)
{
    private const int DayTargetMinutes = 8 * 60;
    private const int WeekTargetMinutes = 40 * 60;

    public async Task<IReadOnlyList<History>> ListAsync(TenantContext context, ObjectId? contractId, int take)
    {
        var filter = OwnedBy(context);
        if (contractId.HasValue)
            filter &= Builders<WorkSessionEntity>.Filter.Eq(x => x.ContractId, contractId.Value);
        var sessions = await database.TimeSessions
            .Find(filter)
            .SortByDescending(x => x.StartTime)
            .Limit(take)
            .ToListAsync();
        if (sessions.Count == 0) return [];
        var contracts = await lookup.ContractsAsync(context.OrganizationId, sessions.Select(x => x.ContractId));
        var projects = await lookup.ProjectsAsync(context.OrganizationId, sessions.Select(x => x.ProjectId));
        var invoiceStatuses =
            await lookup.InvoiceStatusesAsync(context.OrganizationId, sessions.Select(x => x.InvoiceId));
        var now = DateTime.UtcNow;
        return sessions.Select(session => Presentation.ToHistory(
            session,
            contracts.GetValueOrDefault(session.ContractId),
            projects.GetValueOrDefault(session.ProjectId),
            session.InvoiceId.HasValue ? invoiceStatuses.GetValueOrDefault(session.InvoiceId.Value) : null,
            now)).ToList();
    }

    public async Task<IReadOnlyList<Progress>> ProgressAsync(TenantContext context, ObjectId? contractId)
    {
        var filter = OwnedBy(context) & Builders<WorkSessionEntity>.Filter.Ne(x => x.EndTime, null);
        if (contractId.HasValue)
            filter &= Builders<WorkSessionEntity>.Filter.Eq(x => x.ContractId, contractId.Value);
        var sessions = await database.TimeSessions
            .Find(filter)
            .Project(x => new { x.ContractId, x.StartTime, x.EndTime })
            .ToListAsync();
        if (sessions.Count == 0) return [];
        return sessions
            .GroupBy(x => x.ContractId)
            .Select(group => new Progress(
                group.Key.ToString(),
                group.Sum(session => Timing.ElapsedMinutes(session.StartTime, session.EndTime!.Value))))
            .ToList();
    }

    public async Task<TimeBudget> CalculateBudgetAsync(TenantContext context, DateTime dayStart, DateTime dayEnd,
        DateTime weekStart, DateTime weekEnd)
    {
        var rangeStart = weekStart < dayStart ? weekStart : dayStart;
        var rangeEnd = weekEnd > dayEnd ? weekEnd : dayEnd;
        var filter = OwnedBy(context)
                     & Builders<WorkSessionEntity>.Filter.Lt(x => x.StartTime, rangeEnd)
                     & Builders<WorkSessionEntity>.Filter.Or(
                         Builders<WorkSessionEntity>.Filter.Eq(x => x.EndTime, null),
                         Builders<WorkSessionEntity>.Filter.Gt(x => x.EndTime, rangeStart));
        var sessions = await database.TimeSessions
            .Find(filter)
            .Project(x => new { x.StartTime, x.EndTime })
            .ToListAsync();
        var now = DateTime.UtcNow;

        int WorkedMinutes(DateTime from, DateTime to)
        {
            return sessions.Sum(session => Timing.OverlapMinutes(session.StartTime, session.EndTime ?? now, from, to));
        }

        var dayWorkedMinutes = WorkedMinutes(dayStart, dayEnd);
        var weekWorkedMinutes = WorkedMinutes(weekStart, weekEnd);
        return new TimeBudget(
            dayWorkedMinutes,
            DayTargetMinutes,
            Math.Max(0, DayTargetMinutes - dayWorkedMinutes),
            weekWorkedMinutes,
            WeekTargetMinutes,
            Math.Max(0, WeekTargetMinutes - weekWorkedMinutes));
    }

    public async Task<WorkSessionEntity?> FindAsync(TenantContext context, ObjectId sessionId)
    {
        return await database.TimeSessions
            .Find(x => x.Id == sessionId && x.OrganizationId == context.OrganizationId && x.UserId == context.UserId)
            .FirstOrDefaultAsync();
    }

    public async Task<History> ToHistoryAsync(TenantContext context, WorkSessionEntity session, string? invoiceStatus)
    {
        var contractTask = database.BillingContracts
            .Find(x => x.Id == session.ContractId && x.OrganizationId == context.OrganizationId)
            .FirstOrDefaultAsync();
        var projectTask = database.Projects
            .Find(x => x.Id == session.ProjectId && x.OrganizationId == context.OrganizationId)
            .FirstOrDefaultAsync();
        await Task.WhenAll(contractTask, projectTask);
        return Presentation.ToHistory(session, contractTask.Result, projectTask.Result, invoiceStatus,
            DateTime.UtcNow);
    }

    public async Task<string?> LoadInvoiceStatusAsync(TenantContext context, ObjectId? invoiceId)
    {
        if (!invoiceId.HasValue) return null;
        return await database.Invoices
            .Find(x => x.Id == invoiceId.Value && x.OrganizationId == context.OrganizationId)
            .Project(x => x.Status)
            .FirstOrDefaultAsync();
    }

    private static FilterDefinition<WorkSessionEntity> OwnedBy(TenantContext context)
    {
        return Builders<WorkSessionEntity>.Filter.Eq(x => x.OrganizationId, context.OrganizationId)
               & Builders<WorkSessionEntity>.Filter.Eq(x => x.UserId, context.UserId);
    }
}