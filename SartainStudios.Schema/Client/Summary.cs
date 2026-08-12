namespace SartainStudios.Schema.Client;

public sealed record Summary(
    string Id,
    string OrganizationId,
    string CompanyName,
    string ContactPerson,
    Address Address,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);