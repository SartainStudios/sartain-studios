using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Data;

public sealed class Lookup(Database database)
{
    public Task<Dictionary<ObjectId, SartainStudios.Schema.DatabaseEntity.Client>> ClientsAsync(ObjectId organizationId,
        IEnumerable<ObjectId> clientIds)
    {
        return LoadAsync(database.Clients, organizationId, clientIds, x => x.OrganizationId, x => x.Id);
    }

    public Task<Dictionary<ObjectId, SartainStudios.Schema.DatabaseEntity.Project>> ProjectsAsync(
        ObjectId organizationId, IEnumerable<ObjectId> projectIds)
    {
        return LoadAsync(database.Projects, organizationId, projectIds, x => x.OrganizationId, x => x.Id);
    }

    public Task<Dictionary<ObjectId, BillingContract>> ContractsAsync(ObjectId organizationId,
        IEnumerable<ObjectId> contractIds)
    {
        return LoadAsync(database.BillingContracts, organizationId, contractIds, x => x.OrganizationId, x => x.Id);
    }

    public Task<Dictionary<ObjectId, SartainStudios.Schema.DatabaseEntity.Organization>> OrganizationsAsync(
        IEnumerable<ObjectId> organizationIds)
    {
        var unique = organizationIds.Distinct().ToList();
        return unique.Count == 0
            ? Task.FromResult(new Dictionary<ObjectId, SartainStudios.Schema.DatabaseEntity.Organization>())
            : LoadByIdAsync(database.Organizations, unique, x => x.Id);
    }

    public async Task<Dictionary<ObjectId, string>> InvoiceStatusesAsync(ObjectId organizationId,
        IEnumerable<ObjectId?> invoiceIds)
    {
        var unique = invoiceIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (unique.Count == 0) return [];
        var invoices = await database.Invoices
            .Find(x => x.OrganizationId == organizationId && unique.Contains(x.Id))
            .Project(x => new { x.Id, x.Status })
            .ToListAsync();
        return invoices.ToDictionary(x => x.Id, x => x.Status);
    }

    private static async Task<Dictionary<ObjectId, TDocument>> LoadAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        ObjectId organizationId,
        IEnumerable<ObjectId> ids,
        Expression<Func<TDocument, ObjectId>> organizationSelector,
        Expression<Func<TDocument, ObjectId>> idSelector)
    {
        var unique = ids.Distinct().ToList();
        if (unique.Count == 0) return [];
        var filter = Builders<TDocument>.Filter.Eq(organizationSelector, organizationId)
                     & Builders<TDocument>.Filter.In(idSelector, unique);
        var documents = await collection.Find(filter).ToListAsync();
        return documents.ToDictionary(idSelector.Compile());
    }

    private static async Task<Dictionary<ObjectId, TDocument>> LoadByIdAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        IReadOnlyList<ObjectId> ids,
        Expression<Func<TDocument, ObjectId>> idSelector)
    {
        var documents = await collection
            .Find(Builders<TDocument>.Filter.In(idSelector, ids))
            .ToListAsync();
        return documents.ToDictionary(idSelector.Compile());
    }
}