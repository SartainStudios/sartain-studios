namespace SartainStudios.Client.Schema;

public sealed class StoredSession
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public string OrganizationId { get; init; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}