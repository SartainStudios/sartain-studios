using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Organization;
using Metadata = SartainStudios.Client.Page.Metadata;
using Organization = SartainStudios.Client.Service.Organization;

namespace SartainStudios.Client.Layout;

public partial class MainAppBar(
    Authentication authentication,
    Organization organizationClient,
    TokenStore tokenStore,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    PageTitleState pageTitleState,
    ISnackbar snackbar) : ComponentBase, IDisposable
{
    [Parameter] public EventCallback OnDrawerToggle { get; set; }
    private string? CurrentOrganizationId { get; set; }
    private string? CurrentOrganizationName { get; set; }
    private List<OrganizationSummary>? OtherOrganizations { get; set; }
    private bool IsLoadingOrganizations { get; set; }
    private string CurrentPageTitle => pageTitleState.Title;

    public void Dispose()
    {
        authenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        pageTitleState.Changed -= OnPageTitleChanged;
    }

    private Task DrawerToggle()
    {
        return OnDrawerToggle.InvokeAsync();
    }

    protected override void OnInitialized()
    {
        authenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        pageTitleState.Changed += OnPageTitleChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private void OnPageTitleChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> stateTask)
    {
        await InvokeAsync(async () =>
        {
            await RefreshAsync();
            StateHasChanged();
        });
    }

    private async Task RefreshAsync()
    {
        var session = await tokenStore.LoadAsync();
        if (session is null || string.IsNullOrWhiteSpace(session.OrganizationId))
        {
            CurrentOrganizationId = null;
            CurrentOrganizationName = null;
            OtherOrganizations = null;
            return;
        }

        CurrentOrganizationId = session.OrganizationId;
        CurrentOrganizationName = string.IsNullOrWhiteSpace(session.OrganizationName)
            ? "Organization"
            : session.OrganizationName;
        IsLoadingOrganizations = true;
        StateHasChanged();
        try
        {
            var list = await organizationClient.ListMineAsync();
            OtherOrganizations = list
                .Where(o => o.Id != CurrentOrganizationId
                            && string.Equals(o.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            snackbar.Add("Failed to load organizations.", Severity.Error);
            OtherOrganizations = null;
        }
        finally
        {
            IsLoadingOrganizations = false;
            StateHasChanged();
        }
    }

    private async Task SwitchAsync(string organizationId)
    {
        try
        {
            snackbar.Add("Switching organizations...", Severity.Info);
            await organizationClient.SwitchAsync(organizationId);
            navigationManager.NavigateTo(navigationManager.Uri, true);
        }
        catch
        {
            snackbar.Add("Failed to switch organizations.", Severity.Error);
        }
    }

    private async Task SignOutAsync()
    {
        await authentication.SignOutAsync();
        snackbar.Add("Signed out.", Severity.Success);
        navigationManager.NavigateTo(Metadata.Home.IndexRoute, false);
    }
}