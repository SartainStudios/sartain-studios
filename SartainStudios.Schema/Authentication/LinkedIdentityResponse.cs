namespace SartainStudios.Schema.Authentication;

public record LinkedIdentityResponse(IdentityProvider Provider, string? Email, bool EmailVerified, DateTime LinkedAt);