using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Authentication;
using ApiException = SartainStudios.Client.Schema.Api.Exception;

namespace SartainStudios.Client.Page;

public sealed partial class ForgotPassword(Authentication authentication, ISnackbar snackbar)
{
    private string Email { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private Dictionary<string, string[]> FieldErrors { get; set; } = [];
    private bool IsBusy { get; set; }
    private bool IsSubmitted { get; set; }

    private string? FieldError(string field)
    {
        return FieldErrors.TryGetValue(field, out var messages) ? string.Join(" ", messages) : null;
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        FieldErrors = [];
        if (string.IsNullOrWhiteSpace(Email))
        {
            FieldErrors = new Dictionary<string, string[]>
            {
                [AuthenticationErrors.EmailField] = [AuthenticationErrors.EmailRequired]
            };
            return;
        }

        IsBusy = true;
        try
        {
            await authentication.ForgotPasswordAsync(new ForgotPasswordRequest(Email));
            IsSubmitted = true;
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
}