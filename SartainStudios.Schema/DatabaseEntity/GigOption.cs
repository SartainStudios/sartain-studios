using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public abstract class GigOption : AuditableEntity
{
    public ObjectId? UserId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsDefault { get; init; }
}