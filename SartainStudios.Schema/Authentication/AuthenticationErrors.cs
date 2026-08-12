using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.Authentication;

public static class AuthenticationErrors
{
    public const string EmailField = "email";
    public const string PasswordField = "password";
    public const string IdTokenField = "idToken";
    public const string TokenField = "token";
    public const string NewPasswordField = "newPassword";
    public const string RefreshTokenField = "refreshToken";
    public const string EmailRequired = "Email is required.";
    public const string EmailInvalid = "Enter a valid email address.";
    public const string PasswordRequired = "Password is required.";
    public const string IdTokenRequired = "A Google id token is required.";
    public const string ResetTokenRequired = "A password reset token is required.";
    public const string RefreshTokenRequired = "A refresh token is required.";

    public static readonly Error GoogleTokenInvalid = Error.Unauthorized(
        "Authentication.GoogleTokenInvalid",
        "The Google id token could not be validated.");

    public static readonly Error GoogleEmailUnverified = Error.Unauthorized(
        "Authentication.GoogleEmailUnverified",
        "The Google account email must be verified before signing in.");

    public static readonly Error GoogleEmailMissing = Error.Unauthorized(
        "Authentication.GoogleEmailMissing",
        "The Google account did not provide an email address.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Authentication.InvalidCredentials",
        "Invalid email or password.");

    public static readonly Error EmailAlreadyRegistered = Error.Conflict(
        "Authentication.EmailAlreadyRegistered",
        "An account with this email already exists.");

    public static readonly Error RefreshTokenInvalid = Error.Unauthorized(
        "Authentication.RefreshTokenInvalid",
        "The refresh token is invalid or has expired.");

    public static readonly Error SessionExpired = Error.Unauthorized(
        "Authentication.SessionExpired",
        "This session is no longer valid. Please sign in again.");

    public static readonly Error ResetLinkInvalid = Error.Validation(
        "Authentication.ResetLinkInvalid",
        "This password reset link is invalid or has expired.");

    public static readonly Error ResetEmailNotSent = Error.Failure(
        "Authentication.ResetEmailNotSent",
        "The password reset email could not be sent. Please try again.");

    public static string PasswordTooShort(int minimumLength)
    {
        return $"Password must be at least {minimumLength} characters.";
    }

    public static Error ProviderConflict(IdentityProvider existingProvider)
    {
        return Error.Conflict(
            "Authentication.ProviderConflict",
            existingProvider switch
            {
                IdentityProvider.Google =>
                    "This email is already associated with a Google account. Please sign in with Google, then " +
                    "link a password from the linked accounts page if you'd like one.",
                IdentityProvider.Email =>
                    "This email is already associated with a password sign-in account. Please sign in with your " +
                    "email and password, then link your Google account from the linked accounts page.",
                _ => "An account with this email already exists using a different sign-in method."
            });
    }
}