using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using State = SartainStudios.Schema.WorkSession.State;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Timekeeping;

public sealed class Tracker(Database database)
{
    public async Task<WorkSessionEntity?> LoadCurrentAsync(ObjectId organizationId, ObjectId userId)
    {
        return await database.TimeSessions
            .Find(x => x.OrganizationId == organizationId && x.UserId == userId && x.EndTime == null)
            .SortByDescending(x => x.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasOverlapAsync(ObjectId organizationId, ObjectId userId, DateTime intervalStart,
        DateTime intervalEnd, ObjectId? excludeSessionId = null)
    {
        var filter = Builders<WorkSessionEntity>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<WorkSessionEntity>.Filter.Eq(x => x.UserId, userId)
                     & Builders<WorkSessionEntity>.Filter.Lt(x => x.StartTime, intervalEnd)
                     & Builders<WorkSessionEntity>.Filter.Or(
                         Builders<WorkSessionEntity>.Filter.Eq(x => x.EndTime, null),
                         Builders<WorkSessionEntity>.Filter.Gt(x => x.EndTime, intervalStart));
        if (excludeSessionId.HasValue)
            filter &= Builders<WorkSessionEntity>.Filter.Ne(x => x.Id, excludeSessionId.Value);
        return await database.TimeSessions.Find(filter).AnyAsync();
    }

    public async Task<bool> TryStartAsync(WorkSessionEntity session)
    {
        try
        {
            await database.TimeSessions.InsertOneAsync(session);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> TryStopAsync(WorkSessionEntity session, DateTime endTime)
    {
        session.EndTime = endTime;
        session.UpdatedAt = DateTime.UtcNow;
        var result = await database.TimeSessions.ReplaceOneAsync(
            x => x.Id == session.Id && x.EndTime == null,
            session);
        return result.MatchedCount > 0;
    }

    public async Task<(BillingContract? Contract, SartainStudios.Schema.DatabaseEntity.Project? Project)>
        LoadStartTargetsAsync(ObjectId organizationId,
            ObjectId contractId)
    {
        var contract = await database.BillingContracts
            .Find(x => x.Id == contractId && x.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        if (contract is null) return (null, null);
        var project = await database.Projects
            .Find(x => x.Id == contract.ProjectId && x.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        return (contract, project);
    }

    public async Task<State> BuildStateAsync(ObjectId organizationId, WorkSessionEntity? currentSession)
    {
        if (currentSession is null) return Presentation.ToState(null, null, null);
        var contractTask = database.BillingContracts
            .Find(x => x.Id == currentSession.ContractId && x.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        var projectTask = database.Projects
            .Find(x => x.Id == currentSession.ProjectId && x.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        await Task.WhenAll(contractTask, projectTask);
        return Presentation.ToState(currentSession, contractTask.Result, projectTask.Result);
    }
}