namespace SartainStudios.Schema.Membership;

public record Summary(
    string Id,
    string OrganizationId,
    string? UserId,
    string? DisplayName,
    string Email,
    string Role,
    string Status,
    DateTime CreatedAt);