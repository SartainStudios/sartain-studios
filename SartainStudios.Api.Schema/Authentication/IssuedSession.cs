namespace SartainStudios.Api.Schema.Authentication;

public sealed record IssuedSession(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);