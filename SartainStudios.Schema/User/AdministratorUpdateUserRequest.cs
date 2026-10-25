namespace SartainStudios.Schema.User;

public record AdministratorUpdateUserRequest(string DisplayName, string? ProfilePhotoUrl, bool IsAdministrator);