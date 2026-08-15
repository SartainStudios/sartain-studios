using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Schema.Account;
using SartainStudios.Api.Service.Data;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.Membership;

namespace SartainStudios.Api.Service.Account;

public sealed class Deletion(Database database, IMongoClient mongoClient, Draft draftInvoice)
{
    public async Task<DeletionOutcome> DeleteUserAsync(ObjectId userId, TimeZoneInfo userTimeZone)
    {
        var userExists = await database.UserProfiles.Find(x => x.Id == userId).AnyAsync();
        if (!userExists) return DeletionOutcome.UserNotFound;
        using var mongoSession = await mongoClient.StartSessionAsync();
        mongoSession.StartTransaction();
        try
        {
            var memberships = await database.Memberships
                .Find(mongoSession, m => m.UserId == userId)
                .ToListAsync();
            var organizationIdsToDelete = new List<ObjectId>();
            var membershipIdsToRemove = new List<ObjectId>();
            foreach (var membership in memberships)
                if (await IsSoleOwnerAsync(mongoSession, membership, userId))
                    organizationIdsToDelete.Add(membership.OrganizationId);
                else
                    membershipIdsToRemove.Add(membership.Id);
            foreach (var organizationId in organizationIdsToDelete)
                await DeleteOrganizationAsync(mongoSession, organizationId);
            var sharedOrganizationIds = memberships
                .Select(m => m.OrganizationId)
                .Where(organizationId => !organizationIdsToDelete.Contains(organizationId))
                .Distinct()
                .ToList();
            foreach (var organizationId in sharedOrganizationIds)
                await DeleteUserWorkSessionsAsync(mongoSession, organizationId, userId, userTimeZone);
            if (membershipIdsToRemove.Count > 0)
                await database.Memberships.DeleteManyAsync(mongoSession, m => membershipIdsToRemove.Contains(m.Id));
            await database.AuthenticationSessions.DeleteManyAsync(mongoSession, s => s.UserId == userId);
            await database.AuthenticationIdentities.DeleteManyAsync(mongoSession, i => i.UserId == userId);
            await database.EmailPasswordCredentials.DeleteManyAsync(mongoSession, c => c.UserId == userId);
            await database.PasswordResetTokens.DeleteManyAsync(mongoSession, t => t.UserId == userId);
            await database.UserProfiles.DeleteOneAsync(mongoSession, x => x.Id == userId);
            await mongoSession.CommitTransactionAsync();
            return DeletionOutcome.Deleted;
        }
        catch (Exception exception) when (exception is MongoException or InvalidOperationException)
        {
            if (mongoSession.IsInTransaction) await mongoSession.AbortTransactionAsync();
            return DeletionOutcome.Conflict;
        }
    }

    private async Task DeleteOrganizationAsync(IClientSessionHandle mongoSession, ObjectId organizationId)
    {
        await database.TimeSessions.DeleteManyAsync(mongoSession, s => s.OrganizationId == organizationId);
        await database.Invoices.DeleteManyAsync(mongoSession, i => i.OrganizationId == organizationId);
        await database.InvoiceSequences.DeleteManyAsync(mongoSession, s => s.OrganizationId == organizationId);
        await database.BillingContracts.DeleteManyAsync(mongoSession, c => c.OrganizationId == organizationId);
        await database.Projects.DeleteManyAsync(mongoSession, p => p.OrganizationId == organizationId);
        await database.Clients.DeleteManyAsync(mongoSession, c => c.OrganizationId == organizationId);
        await database.Memberships.DeleteManyAsync(mongoSession, m => m.OrganizationId == organizationId);
        await database.Organizations.DeleteOneAsync(mongoSession, o => o.Id == organizationId);
    }

    private async Task<bool> IsSoleOwnerAsync(IClientSessionHandle mongoSession,
        SartainStudios.Schema.DatabaseEntity.Membership membership,
        ObjectId userId)
    {
        if (membership.Role != nameof(RoleType.Owner) || membership.Status != nameof(RoleStatus.Active))
            return false;
        var hasOtherActiveOwner = await database.Memberships
            .Find(mongoSession, m => m.OrganizationId == membership.OrganizationId
                                     && m.Role == nameof(RoleType.Owner)
                                     && m.Status == nameof(RoleStatus.Active)
                                     && m.UserId != userId)
            .AnyAsync();
        return !hasOtherActiveOwner;
    }

    private async Task DeleteUserWorkSessionsAsync(
        IClientSessionHandle mongoSession,
        ObjectId organizationId,
        ObjectId userId,
        TimeZoneInfo userTimeZone)
    {
        var sessions = await database.TimeSessions
            .Find(mongoSession, s => s.OrganizationId == organizationId && s.UserId == userId)
            .ToListAsync();
        if (sessions.Count == 0) return;
        var invoiceIds = sessions
            .Where(s => s.InvoiceId.HasValue)
            .Select(s => s.InvoiceId!.Value)
            .Distinct()
            .ToList();
        await database.TimeSessions.DeleteManyAsync(mongoSession,
            s => s.OrganizationId == organizationId && s.UserId == userId);
        foreach (var invoiceId in invoiceIds)
        {
            var invoice = await draftInvoice.LoadAsync(organizationId, invoiceId, mongoSession);
            if (invoice is null) continue;
            await draftInvoice.RecalculateOrDeleteAsync(mongoSession, invoice, userTimeZone);
        }
    }
}