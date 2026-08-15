using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Invoice;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Timekeeping;

public sealed class Editing(Database database, IMongoClient mongoClient, Draft draftInvoice)
{
    public async Task<bool> TryReplaceUnbilledAsync(WorkSessionEntity session)
    {
        var result = await database.TimeSessions.ReplaceOneAsync(
            x => x.Id == session.Id && x.InvoiceId == null,
            session);
        return result.MatchedCount > 0;
    }

    public async Task<bool> TryReplaceOnDraftInvoiceAsync(
        WorkSessionEntity session,
        InvoiceEntity invoice,
        TimeZoneInfo userTimeZone)
    {
        using var mongoTransaction = await mongoClient.StartSessionAsync();
        mongoTransaction.StartTransaction();
        try
        {
            var result = await database.TimeSessions.ReplaceOneAsync(
                mongoTransaction,
                x => x.Id == session.Id && x.InvoiceId == invoice.Id,
                session);
            if (result.MatchedCount == 0)
            {
                await mongoTransaction.AbortTransactionAsync();
                return false;
            }

            await draftInvoice.RecalculateOrDeleteAsync(mongoTransaction, invoice, userTimeZone);
            await mongoTransaction.CommitTransactionAsync();
            return true;
        }
        catch (MongoException)
        {
            if (mongoTransaction.IsInTransaction) await mongoTransaction.AbortTransactionAsync();
            return false;
        }
    }

    public Task DeleteUnbilledAsync(WorkSessionEntity session)
    {
        return database.TimeSessions.DeleteOneAsync(x => x.Id == session.Id);
    }

    public async Task<bool> TryDiscardFromDraftInvoiceAsync(
        WorkSessionEntity session,
        InvoiceEntity invoice,
        TimeZoneInfo userTimeZone)
    {
        using var mongoTransaction = await mongoClient.StartSessionAsync();
        mongoTransaction.StartTransaction();
        try
        {
            var deleteResult = await database.TimeSessions.DeleteOneAsync(
                mongoTransaction,
                x => x.Id == session.Id && x.InvoiceId == invoice.Id);
            if (deleteResult.DeletedCount == 0)
            {
                await mongoTransaction.AbortTransactionAsync();
                return false;
            }

            var remaining = await database.TimeSessions
                .Find(mongoTransaction, x => x.InvoiceId == invoice.Id && x.OrganizationId == invoice.OrganizationId)
                .AnyAsync();
            if (remaining)
                await draftInvoice.RecalculateOrDeleteAsync(mongoTransaction, invoice, userTimeZone);
            else
                await database.Invoices.DeleteOneAsync(mongoTransaction, x => x.Id == invoice.Id);
            await mongoTransaction.CommitTransactionAsync();
            return true;
        }
        catch (MongoException)
        {
            if (mongoTransaction.IsInTransaction) await mongoTransaction.AbortTransactionAsync();
            return false;
        }
    }
}