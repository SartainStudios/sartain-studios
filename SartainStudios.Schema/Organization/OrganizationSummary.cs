namespace SartainStudios.Schema.Organization;

public record OrganizationSummary(
    string Id,
    string Name,
    Address? Address,
    string Email,
    string? PhoneNumber,
    string Role,
    string MembershipStatus,
    bool IsActive);