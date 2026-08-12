namespace SartainStudios.Schema.Authentication;

public record CurrentUserResponse(
    string UserId,
    string OrganizationId,
    string? DisplayName,
    string? Email,
    string? Role);