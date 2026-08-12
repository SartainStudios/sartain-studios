using SartainStudios.Schema.Api;

namespace SartainStudios.Schema.User;

public static class AccountErrors
{
    public const string DisplayNameField = "displayName";
    public const string ProfilePhotoUrlField = "profilePhotoUrl";
    public const string WeeklyHourLimitMinutesField = "weeklyHourLimitMinutes";
    public const string HourLimitWarningMinutesField = "hourLimitWarningMinutes";
    public const string CurrentPasswordField = "currentPassword";
    public const string NewPasswordField = "newPassword";
    public const string ProviderField = "provider";
    public const int DisplayNameMaximumLength = 100;
    public const int ProfilePhotoUrlMaximumLength = 2048;
    public const int WeeklyHourLimitMaximumMinutes = 10080;
    public const int MinimumPasswordLength = 8;
    public const string DisplayNameRequired = "Display name is required.";
    public const string ProfilePhotoUrlInvalid = "Profile photo must be a valid http or https URL.";
    public const string WeeklyHourLimitOutOfRange = "Weekly hour limit must be between 1 minute and 7 days.";
    public const string HourLimitWarningNegative = "Warning threshold cannot be negative.";

    public const string HourLimitWarningExceedsLimit =
        "Warning threshold cannot be greater than the weekly hour limit.";

    public const string CurrentPasswordRequired = "Current password is required.";
    public const string CurrentPasswordIncorrect = "Current password is incorrect.";
    public const string NewPasswordMatchesCurrent = "New password must be different from the current password.";
    public const string ProviderUnknown = "Unknown sign-in provider.";

    public static readonly string DisplayNameTooLong =
        $"Display name cannot exceed {DisplayNameMaximumLength} characters.";

    public static readonly string ProfilePhotoUrlTooLong =
        $"Profile photo URL cannot exceed {ProfilePhotoUrlMaximumLength} characters.";

    public static readonly Error NotResolved = Error.Unauthorized(
        "Account.NotResolved",
        "The signed-in account could not be resolved.");

    public static readonly Error IdentityNotLinked = Error.NotFound(
        "Account.IdentityNotLinked",
        "That sign-in method is not linked to this account.");

    public static readonly Error LastSignInMethod = Error.Validation(
        "Account.LastSignInMethod",
        "Cannot unlink the only sign-in method.");

    public static readonly Error EmailAlreadyUsed = Error.Conflict(
        "Account.EmailAlreadyUsed",
        "Another account already uses this email for password sign-in.");

    public static readonly Error DeletionConflict = Error.Conflict(
        "Account.DeletionConflict",
        "Account deletion failed due to a data conflict. Please try again.");

    public static string NewPasswordTooShort(int minimumLength)
    {
        return $"New password must be at least {minimumLength} characters.";
    }
}