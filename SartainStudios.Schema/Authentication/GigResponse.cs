namespace SartainStudios.Schema.Authentication;

public record GigResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    bool IsNewUser,
    User User);