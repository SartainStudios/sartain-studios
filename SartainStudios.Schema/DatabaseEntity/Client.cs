using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class Client : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}