using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Schema.User;
using UserAdministrationService = SartainStudios.Client.Service.UserAdministration;

namespace SartainStudios.Client.Page;

public sealed partial class UserAdministration(
    UserAdministrationService userAdministration,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private string? _currentUserId;

    private bool IsAuthorized { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsBusy { get; set; }
    private string? ErrorMessage { get; set; }
    private string? EditErrorMessage { get; set; }
    private string SearchText { get; set; } = string.Empty;
    private List<AdministratorUserResponse> Users { get; set; } = [];
    private AdministratorUserResponse? Selected { get; set; }
    private bool EditVisible { get; set; }
    private bool DeleteVisible { get; set; }

    private IEnumerable<AdministratorUserResponse> FilteredUsers =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Users
            : Users.Where(user =>
                user.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || user.Emails.Any(email => email.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsAuthorized = await userAdministration.HasAccessAsync();
        }
        catch (Exception)
        {
            IsAuthorized = false;
        }

        if (!IsAuthorized)
        {
            navigationManager.NavigateTo(Metadata.NotFound.Route, false, true);
            return;
        }

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = state.User.FindFirst("sub")?.Value;
        await LoadAsync();
    }

    private bool IsSelf(AdministratorUserResponse user)
    {
        return !string.IsNullOrEmpty(_currentUserId) && user.Id == _currentUserId;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Users = [.. await userAdministration.ListAsync()];
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenEdit(AdministratorUserResponse user)
    {
        Selected = user;
        EditErrorMessage = null;
        EditVisible = true;
    }

    private void OpenDelete(AdministratorUserResponse user)
    {
        Selected = user;
        DeleteVisible = true;
    }

    private void OnEditVisibleChanged(bool visible)
    {
        EditVisible = visible;
    }

    private void OnDeleteVisibleChanged(bool visible)
    {
        DeleteVisible = visible;
    }

    private async Task SaveAsync(AdministratorUpdateUserRequest request)
    {
        if (Selected is null)
            return;
        IsBusy = true;
        EditErrorMessage = null;
        try
        {
            var updated = await userAdministration.UpdateAsync(Selected.Id, request);
            var index = Users.FindIndex(user => user.Id == updated.Id);
            if (index >= 0)
                Users[index] = updated;
            EditVisible = false;
            snackbar.Add("User updated.", Severity.Success);
        }
        catch (Exception exception)
        {
            EditErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (Selected is null)
            return;
        IsBusy = true;
        try
        {
            await userAdministration.DeleteAsync(Selected.Id);
            Users.RemoveAll(user => user.Id == Selected.Id);
            DeleteVisible = false;
            snackbar.Add("User deleted.", Severity.Success);
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}