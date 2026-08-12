namespace SartainStudios.Schema.Authentication;

public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);