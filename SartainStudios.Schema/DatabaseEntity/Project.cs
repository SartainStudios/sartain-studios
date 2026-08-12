using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class Project : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}