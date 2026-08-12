namespace SartainStudios.Schema.Authentication;

public record GoogleSignInRequest(
    string IdToken,
    string? OrganizationName,
    Address? OrganizationAddress,
    string? OrganizationEmail,
    string? OrganizationPhoneNumber);