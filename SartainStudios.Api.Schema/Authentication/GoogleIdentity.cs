namespace SartainStudios.Api.Schema.Authentication;

public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string DisplayName,
    string? ProfilePhotoUrl);