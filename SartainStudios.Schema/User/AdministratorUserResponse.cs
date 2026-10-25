namespace SartainStudios.Schema.User;

public record AdministratorUserResponse(
    string Id,
    string DisplayName,
    string? ProfilePhotoUrl,
    IReadOnlyList<string> Emails,
    bool IsAdministrator,
    DateTime CreatedAt);