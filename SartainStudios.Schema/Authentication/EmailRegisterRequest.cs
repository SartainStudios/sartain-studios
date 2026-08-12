namespace SartainStudios.Schema.Authentication;

public record EmailRegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    string? OrganizationName,
    Address? OrganizationAddress,
    string? OrganizationEmail,
    string? OrganizationPhoneNumber);