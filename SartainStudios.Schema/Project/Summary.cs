namespace SartainStudios.Schema.Project;

public sealed record Summary(
    string Id,
    string OrganizationId,
    string ClientId,
    string ClientCompanyName,
    string Name,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);