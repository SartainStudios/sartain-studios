using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class PasswordResetToken : AuditableEntity
{
    public ObjectId UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? UsedAt { get; set; }
}