using MongoDB.Bson;

namespace SartainStudios.Api.Schema.Authentication;

public readonly record struct TenantContext(ObjectId UserId, ObjectId OrganizationId);