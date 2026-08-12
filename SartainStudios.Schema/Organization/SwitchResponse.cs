namespace SartainStudios.Schema.Organization;

public record SwitchResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    Authentication.User User,
    Authentication.Organization Organization);