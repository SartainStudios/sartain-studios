namespace SartainStudios.Schema.Authentication;

public record ResetPasswordRequest(string Token, string NewPassword);