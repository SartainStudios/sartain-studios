using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Authentication;
using ApiException = SartainStudios.Client.Schema.Api.Exception;

namespace SartainStudios.Client.Page;

public sealed partial class SignIn(
    Authentication authentication,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private string Email { get; set; } = string.Empty;
    private string Password { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private Dictionary<string, string[]> FieldErrors { get; set; } = [];
    private bool IsBusy { get; set; }

    private string ReturnUrl
    {
        get
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            var target = ExtractReturnUrl(uri.Query);
            return string.IsNullOrWhiteSpace(target) ? Metadata.MainMenu.IndexRoute : target;
        }
    }

    private string? FieldError(string field)
    {
        return FieldErrors.TryGetValue(field, out var messages) ? string.Join(" ", messages) : null;
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        FieldErrors = [];
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your email address and password.";
            snackbar.Add(ErrorMessage, Severity.Error);
            return;
        }

        IsBusy = true;
        try
        {
            await ContinueAsync();
            navigationManager.NavigateTo(ReturnUrl);
        }
        catch (ApiException exception)
        {
            HandleFailure(exception);
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

    private async Task ContinueAsync()
    {
        try
        {
            await authentication.SignInAsync(new EmailSignInRequest(Email, Password));
        }
        catch (ApiException exception) when (exception.Code == AuthenticationErrors.InvalidCredentials.Code)
        {
            try
            {
                snackbar.Add("Creating your account...", Severity.Info);
                await authentication.RegisterAsync(
                    new EmailRegisterRequest(Email, Password, null, null, null, null, null));
            }
            catch (ApiException registerException)
                when (registerException.Code == AuthenticationErrors.EmailAlreadyRegistered.Code)
            {
                throw exception;
            }
        }
    }

    private void HandleFailure(ApiException exception)
    {
        FieldErrors = exception.Errors.ToDictionary(entry => entry.Key, entry => entry.Value);
        ErrorMessage = exception.Message;
        snackbar.Add(ErrorMessage, Severity.Error);
    }

    private static string? ExtractReturnUrl(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return (from pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            select pair.Split('=', 2)
            into parts
            where parts.Length == 2 && string.Equals(parts[0], "returnUrl", StringComparison.OrdinalIgnoreCase)
            select Uri.UnescapeDataString(parts[1])).FirstOrDefault();
    }
}