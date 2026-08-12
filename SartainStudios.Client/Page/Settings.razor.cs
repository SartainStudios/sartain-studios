using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.User;
using AccountModel = SartainStudios.Schema.Authentication.AccountResponse;
using AccountService = SartainStudios.Client.Service.Authentication.Account;
using ApiException = SartainStudios.Client.Schema.Api.Exception;
using AuthenticationService = SartainStudios.Client.Service.Authentication.Authentication;

namespace SartainStudios.Client.Page;

public sealed partial class Settings(
    AccountService accountClient,
    AuthenticationService authentication,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private const int ProfileTabIndex = 0;
    private const int ProvidersTabIndex = 1;
    private const int PasswordTabIndex = 2;
    private const string ConfirmPasswordField = "confirmPassword";
    private const int DefaultWeeklyHourLimitMinutes = 2400;

    [Parameter]
    [SupplyParameterFromQuery(Name = "tab")]
    public string? Tab { get; set; }

    private int ActiveTabIndex { get; set; }
    private AccountModel? Account { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private Dictionary<string, string[]> FieldErrors { get; set; } = [];
    private string DisplayName { get; set; } = string.Empty;
    private string ProfilePhotoUrl { get; set; } = string.Empty;
    private bool HourLimitEnabled { get; set; }
    private double WeeklyHourLimitHours { get; set; } = 40;
    private int HourLimitWarningMinutes { get; set; } = 30;
    private bool IsSavingNotificationPreferences { get; set; }
    private string CurrentPassword { get; set; } = string.Empty;
    private string NewPassword { get; set; } = string.Empty;
    private string ConfirmPassword { get; set; } = string.Empty;
    private int PasswordFormKey { get; set; }

    private bool CanSubmitPassword =>
        !string.IsNullOrWhiteSpace(NewPassword)
        && !string.IsNullOrWhiteSpace(ConfirmPassword)
        && (Account?.HasPassword != true || !string.IsNullOrWhiteSpace(CurrentPassword));

    private static int MinimumPasswordLength => AccountErrors.MinimumPasswordLength;
    private bool IsConfirmingDelete { get; set; }
    private string DeleteConfirmationText { get; set; } = string.Empty;
    private string? DeleteErrorMessage { get; set; }
    private bool IsDeleting { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override void OnParametersSet()
    {
        ActiveTabIndex = Tab?.ToLowerInvariant() switch
        {
            "providers" => ProvidersTabIndex,
            "password" => PasswordTabIndex,
            _ => ProfileTabIndex
        };
    }

    private Task OnTabChangedAsync(int index)
    {
        ActiveTabIndex = index;
        var route = index switch
        {
            ProvidersTabIndex => Metadata.Account.ProvidersRoute,
            PasswordTabIndex => Metadata.Account.PasswordRoute,
            _ => Metadata.Account.ProfileRoute
        };
        navigationManager.NavigateTo(route, false, true);
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        try
        {
            Account = await accountClient.GetAsync();
            ApplyAccount(Account);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    private void ApplyAccount(AccountModel account)
    {
        DisplayName = account.User.DisplayName;
        ProfilePhotoUrl = account.User.ProfilePhotoUrl ?? string.Empty;
        HourLimitEnabled = account.WeeklyHourLimitMinutes.HasValue;
        WeeklyHourLimitHours = (account.WeeklyHourLimitMinutes ?? DefaultWeeklyHourLimitMinutes) / 60.0;
        HourLimitWarningMinutes = account.HourLimitWarningMinutes;
    }

    private async Task SaveProfileAsync()
    {
        ResetErrors();
        IsBusy = true;
        try
        {
            Account = await accountClient.UpdateProfileAsync(
                new UpdateProfileRequest(
                    DisplayName,
                    string.IsNullOrWhiteSpace(ProfilePhotoUrl) ? null : ProfilePhotoUrl));
            ApplyAccount(Account);
            snackbar.Add("Profile updated.", Severity.Success);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveNotificationPreferencesAsync()
    {
        ResetErrors();
        IsSavingNotificationPreferences = true;
        try
        {
            int? weeklyHourLimitMinutes = HourLimitEnabled
                ? (int)Math.Round(WeeklyHourLimitHours * 60)
                : null;
            Account = await accountClient.UpdateNotificationPreferencesAsync(
                new NotificationPreferencesRequest(weeklyHourLimitMinutes, HourLimitWarningMinutes));
            ApplyAccount(Account);
            snackbar.Add("Notification preferences updated.", Severity.Success);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            IsSavingNotificationPreferences = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        ResetErrors();
        if (NewPassword != ConfirmPassword)
        {
            FieldErrors[ConfirmPasswordField] = ["Passwords do not match."];
            snackbar.Add("Passwords do not match.", Severity.Error);
            return;
        }

        IsBusy = true;
        try
        {
            Account = await accountClient.ChangePasswordAsync(new ChangePasswordRequest(
                Account?.HasPassword == true ? CurrentPassword : null,
                NewPassword));
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
            PasswordFormKey++;
            snackbar.Add("Password updated.", Severity.Success);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UnlinkAsync(IdentityProvider provider)
    {
        ResetErrors();
        IsBusy = true;
        try
        {
            Account = await accountClient.UnlinkProviderAsync(provider);
            snackbar.Add("Sign-in method unlinked.", Severity.Success);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelDelete()
    {
        IsConfirmingDelete = false;
        DeleteConfirmationText = string.Empty;
        DeleteErrorMessage = null;
    }

    private async Task DeleteAccountAsync()
    {
        if (DeleteConfirmationText != "DELETE") return;
        IsDeleting = true;
        DeleteErrorMessage = null;
        try
        {
            await accountClient.DeleteAccountAsync();
            snackbar.Add("Your account has been deleted.", Severity.Success);
            await authentication.SignOutAsync();
            navigationManager.NavigateTo(Metadata.Account.SignInRoute, true);
        }
        catch (Exception exception)
        {
            DeleteErrorMessage = exception.Message;
            snackbar.Add(exception.Message, Severity.Error);
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private void HandleFailure(Exception exception)
    {
        if (exception is ApiException apiException)
            FieldErrors = apiException.Errors.ToDictionary(entry => entry.Key, entry => entry.Value);
        ErrorMessage = exception.Message;
        snackbar.Add(exception.Message, Severity.Error);
    }

    private void ResetErrors()
    {
        ErrorMessage = null;
        FieldErrors = [];
    }

    private bool HasFieldError(string field)
    {
        return FieldErrors.ContainsKey(field);
    }

    private string? FieldError(string field)
    {
        return FieldErrors.TryGetValue(field, out var messages) && messages.Length > 0
            ? string.Join(" ", messages)
            : null;
    }

    private static string FormatProvider(IdentityProvider provider)
    {
        return provider switch
        {
            IdentityProvider.Google => "Google",
            IdentityProvider.Email => "Email & password",
            _ => provider.ToString()
        };
    }

    private static string GetIcon(IdentityProvider provider)
    {
        return provider switch
        {
            IdentityProvider.Google => Icons.Material.Filled.Public,
            IdentityProvider.Email => Icons.Material.Filled.Mail,
            _ => Icons.Material.Filled.VpnKey
        };
    }

    private static string GetInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }
}