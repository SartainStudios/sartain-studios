using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class AuthenticationSession : AuditableEntity
{
    public ObjectId UserId { get; init; }
    public ObjectId OrganizationId { get; init; }
    [BsonRepresentation(BsonType.String)] public IdentityProvider Provider { get; init; }
    public string RefreshTokenHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; set; }
}