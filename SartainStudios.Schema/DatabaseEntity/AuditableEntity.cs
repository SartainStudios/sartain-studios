using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public abstract class AuditableEntity
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}