using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class WorkSession : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId UserId { get; init; }
    public ObjectId ContractId { get; init; }
    public ObjectId ProjectId { get; init; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ObjectId? InvoiceId { get; set; }
}