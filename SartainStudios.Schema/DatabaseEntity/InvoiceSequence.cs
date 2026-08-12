using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class InvoiceSequence : AuditableEntity
{
    public ObjectId OrganizationId { get; set; }
    public string InvoicePrefix { get; set; } = string.Empty;
    public int Sequence { get; set; }
}