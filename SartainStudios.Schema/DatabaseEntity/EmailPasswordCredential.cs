using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class EmailPasswordCredential : AuditableEntity
{
    public ObjectId UserId { get; init; }
    public string PasswordHash { get; set; } = string.Empty;
}