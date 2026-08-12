using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Invoice;

public sealed class Assignment(Database database)
{
    public async Task<IReadOnlyList<WorkSession>> LoadBilledSessionsAsync(ObjectId organizationId, ObjectId invoiceId)
    {
        return await database.TimeSessions
            .Find(x => x.InvoiceId == invoiceId && x.OrganizationId == organizationId)
            .SortBy(x => x.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkSession>> LoadBillableSessionsAsync(ObjectId organizationId,
        ObjectId contractId, ObjectId? invoiceId = null)
    {
        var filter = Builders<WorkSession>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<WorkSession>.Filter.Eq(x => x.ContractId, contractId)
                     & Builders<WorkSession>.Filter.Ne(x => x.EndTime, null);
        filter &= invoiceId.HasValue
            ? Builders<WorkSession>.Filter.Or(
                Builders<WorkSession>.Filter.Eq(x => x.InvoiceId, null),
                Builders<WorkSession>.Filter.Eq(x => x.InvoiceId, invoiceId.Value))
            : Builders<WorkSession>.Filter.Eq(x => x.InvoiceId, null);
        return await database.TimeSessions
            .Find(filter)
            .SortBy(x => x.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkSession>> LoadSelectedSessionsAsync(IClientSessionHandle mongoSession,
        ObjectId organizationId, ObjectId contractId, IReadOnlyList<ObjectId> sessionIds)
    {
        return await database.TimeSessions
            .Find(mongoSession,
                x => x.OrganizationId == organizationId && x.ContractId == contractId && sessionIds.Contains(x.Id))
            .SortBy(x => x.StartTime)
            .ToListAsync();
    }

    public async Task<bool> AssignAsync(IClientSessionHandle mongoSession, ObjectId organizationId, ObjectId invoiceId,
        IReadOnlyList<ObjectId> sessionIds, DateTime timestamp)
    {
        if (sessionIds.Count == 0) return true;
        var result = await database.TimeSessions.UpdateManyAsync(
            mongoSession,
            Builders<WorkSession>.Filter.Eq(x => x.OrganizationId, organizationId)
            & Builders<WorkSession>.Filter.In(x => x.Id, sessionIds)
            & Builders<WorkSession>.Filter.Eq(x => x.InvoiceId, null),
            Builders<WorkSession>.Update
                .Set(x => x.InvoiceId, invoiceId)
                .Set(x => x.UpdatedAt, timestamp));
        return result.MatchedCount == sessionIds.Count;
    }

    public async Task ReleaseAsync(IClientSessionHandle mongoSession, ObjectId organizationId, ObjectId invoiceId,
        IReadOnlyList<ObjectId> sessionIds, DateTime timestamp)
    {
        if (sessionIds.Count == 0) return;
        await database.TimeSessions.UpdateManyAsync(
            mongoSession,
            Builders<WorkSession>.Filter.Eq(x => x.OrganizationId, organizationId)
            & Builders<WorkSession>.Filter.In(x => x.Id, sessionIds)
            & Builders<WorkSession>.Filter.Eq(x => x.InvoiceId, invoiceId),
            Builders<WorkSession>.Update
                .Set(x => x.InvoiceId, null)
                .Set(x => x.UpdatedAt, timestamp));
    }
}