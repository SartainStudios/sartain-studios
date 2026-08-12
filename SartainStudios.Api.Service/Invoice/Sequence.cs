using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;

namespace SartainStudios.Api.Service.Invoice;

public sealed partial class Sequence(Database database)
{
    private const int InitialSequence = 0;

    public async Task InitializeAsync(ObjectId organizationId, string invoicePrefix)
    {
        var prefix = invoicePrefix.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;
        var filter = Builders<InvoiceSequence>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<InvoiceSequence>.Filter.Eq(x => x.InvoicePrefix, prefix);
        var update = Builders<InvoiceSequence>.Update
            .SetOnInsert(x => x.OrganizationId, organizationId)
            .SetOnInsert(x => x.InvoicePrefix, prefix)
            .SetOnInsert(x => x.Sequence, InitialSequence)
            .SetOnInsert(x => x.CreatedAt, now)
            .SetOnInsert(x => x.UpdatedAt, now);
        await database.InvoiceSequences.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task<InvoiceSequence?> AllocateAsync(IClientSessionHandle mongoSession, ObjectId organizationId,
        string invoicePrefix)
    {
        var prefix = invoicePrefix.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;
        var filter = Builders<InvoiceSequence>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<InvoiceSequence>.Filter.Eq(x => x.InvoicePrefix, prefix);
        var update = Builders<InvoiceSequence>.Update
            .SetOnInsert(x => x.OrganizationId, organizationId)
            .SetOnInsert(x => x.InvoicePrefix, prefix)
            .SetOnInsert(x => x.CreatedAt, now)
            .Inc(x => x.Sequence, 1)
            .Set(x => x.UpdatedAt, now);
        return await database.InvoiceSequences.FindOneAndUpdateAsync(
            mongoSession,
            filter,
            update,
            new FindOneAndUpdateOptions<InvoiceSequence>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            });
    }

    public static string BuildInvoiceNumber(string invoicePrefix, InvoiceSequence sequence)
    {
        return $"{invoicePrefix.Trim().ToUpperInvariant()}{sequence.Sequence.ToString(CultureInfo.InvariantCulture)}";
    }

    public async Task TryRollBackAsync(IClientSessionHandle mongoSession, ObjectId organizationId,
        InvoiceEntity invoice)
    {
        var match = InvoiceNumberPattern().Match(invoice.InvoiceNumber);
        if (!match.Success) return;
        var prefix = match.Groups["prefix"].Value.Trim().ToUpperInvariant();
        if (!int.TryParse(match.Groups["sequence"].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var parsedSequence)) return;
        var filter = Builders<InvoiceSequence>.Filter.Eq(x => x.OrganizationId, organizationId)
                     & Builders<InvoiceSequence>.Filter.Eq(x => x.InvoicePrefix, prefix)
                     & Builders<InvoiceSequence>.Filter.Eq(x => x.Sequence, parsedSequence);
        var update = Builders<InvoiceSequence>.Update
            .Inc(x => x.Sequence, -1)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await database.InvoiceSequences.UpdateOneAsync(mongoSession, filter, update);
    }

    [GeneratedRegex(@"^(?<prefix>.*?)(?<sequence>\d+)$")]
    private static partial Regex InvoiceNumberPattern();
}