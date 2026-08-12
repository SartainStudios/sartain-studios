namespace SartainStudios.Schema.Authentication;

public record Response(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    bool IsNewUser,
    User User,
    Organization Organization);