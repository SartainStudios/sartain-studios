using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Authentication;
using SartainStudios.Schema.User;
using ApiException = SartainStudios.Client.Schema.Api.Exception;

namespace SartainStudios.Client.Page;

public sealed partial class ResetPassword(
    Authentication authentication,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    internal const int MinimumPasswordLength = AccountErrors.MinimumPasswordLength;
    private const string ConfirmPasswordField = "confirmPassword";
    private string NewPassword { get; set; } = string.Empty;
    private string ConfirmPassword { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private Dictionary<string, string[]> FieldErrors { get; set; } = [];
    private bool IsBusy { get; set; }
    private bool IsSubmitted { get; set; }
    private string? Token { get; set; }

    protected override void OnInitialized()
    {
        var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
        Token = ExtractToken(uri.Query);
    }

    private string? FieldError(string field)
    {
        return FieldErrors.TryGetValue(field, out var messages) ? string.Join(" ", messages) : null;
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        FieldErrors = [];
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = AuthenticationErrors.ResetLinkInvalid.Description;
            snackbar.Add(ErrorMessage, Severity.Error);
            return;
        }

        if (!TryValidate()) return;
        IsBusy = true;
        try
        {
            await authentication.ResetPasswordAsync(new ResetPasswordRequest(Token, NewPassword));
            IsSubmitted = true;
            snackbar.Add("Password updated. You can now sign in.", Severity.Success);
        }
        catch (ApiException exception)
        {
            FieldErrors = exception.Errors.ToDictionary(entry => entry.Key, entry => entry.Value);
            ErrorMessage = exception.Message;
            snackbar.Add(ErrorMessage, Severity.Error);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            snackbar.Add(ErrorMessage, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate()
    {
        var errors = new Dictionary<string, string[]>();
        if (NewPassword.Length < MinimumPasswordLength)
            errors[AuthenticationErrors.NewPasswordField] =
                [AuthenticationErrors.PasswordTooShort(MinimumPasswordLength)];
        if (NewPassword != ConfirmPassword)
            errors[ConfirmPasswordField] = ["Passwords do not match."];
        FieldErrors = errors;

        return errors.Count == 0;
    }

    private static string? ExtractToken(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return (from pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            select pair.Split('=', 2)
            into parts
            where parts.Length == 2 && string.Equals(parts[0], "token", StringComparison.OrdinalIgnoreCase)
            select Uri.UnescapeDataString(parts[1])).FirstOrDefault();
    }
}