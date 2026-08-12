using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SartainStudios.Schema.Authentication;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class AuthenticationIdentity : AuditableEntity
{
    public ObjectId UserId { get; init; }
    [BsonRepresentation(BsonType.String)] public IdentityProvider Provider { get; init; }
    public string ProviderSubject { get; init; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
}