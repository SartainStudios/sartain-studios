using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Layout;

public partial class GigAppBar(
    Authentication authentication,
    TokenStore tokenStore,
    NavigationManager navigationManager,
    PageTitleState pageTitleState,
    ISnackbar snackbar) : ComponentBase, IDisposable
{
    [Parameter] public EventCallback OnDrawerToggle { get; set; }

    private string CurrentPageTitle => pageTitleState.Title;
    private string? DisplayName { get; set; }
    private string? ProfilePhotoUrl { get; set; }

    public void Dispose()
    {
        pageTitleState.Changed -= OnPageTitleChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        pageTitleState.Changed += OnPageTitleChanged;
        var session = await tokenStore.LoadAsync();
        DisplayName = session?.DisplayName;
        ProfilePhotoUrl = session?.ProfilePhotoUrl;
    }

    private Task DrawerToggle()
    {
        return OnDrawerToggle.InvokeAsync();
    }

    private void OnPageTitleChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task SignOutAsync()
    {
        await authentication.SignOutAsync();
        snackbar.Add("Signed out.", Severity.Success);
        navigationManager.NavigateTo(Metadata.MainMenu.IndexRoute, false);
    }
}