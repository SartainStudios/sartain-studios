using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Layout;

public partial class SectionAppBar(
    Authentication authentication,
    TokenStore tokenStore,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    PageTitleState pageTitleState,
    ISnackbar snackbar) : ComponentBase, IDisposable
{
    [Parameter] public EventCallback OnDrawerToggle { get; set; }
    [Parameter] public string HomeIcon { get; set; } = string.Empty;
    [Parameter] public string HomeRoute { get; set; } = string.Empty;
    [Parameter] public RenderFragment? AccountMenuItems { get; set; }

    private string CurrentPageTitle => pageTitleState.Title;
    private string? DisplayName { get; set; }

    public void Dispose()
    {
        authenticationStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        pageTitleState.Changed -= OnPageTitleChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        authenticationStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
        pageTitleState.Changed += OnPageTitleChanged;
        await LoadDisplayNameAsync();
    }

    private async Task LoadDisplayNameAsync()
    {
        var session = await tokenStore.LoadAsync();
        DisplayName = session?.DisplayName;
    }

    private Task DrawerToggle()
    {
        return OnDrawerToggle.InvokeAsync();
    }

    private void OnPageTitleChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> stateTask)
    {
        await InvokeAsync(async () =>
        {
            await LoadDisplayNameAsync();
            StateHasChanged();
        });
    }

    private async Task SignOutAsync()
    {
        await authentication.SignOutAsync();
        snackbar.Add("Signed out.", Severity.Success);
        navigationManager.NavigateTo(Metadata.MainMenu.IndexRoute, false);
    }
}